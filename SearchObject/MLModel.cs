using Microsoft.ML;
using Microsoft.ML.Data;

namespace SearchObject
{
    public class MLModel
    {
        private PredictionEngine<ModelInput, ModelOutput> _predict;

        public Task LoadModelAsync(string path)
        {
            return Task.Run(() =>
            {
                var mlContext = new MLContext();
                ITransformer mlModel = mlContext.Model.Load(path, out var _);
                _predict = mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(mlModel);
            });
        }

        public Task<ModelOutput> Predict(string path)
        {
            return Task.Run(() =>
            {
                ModelInput input = new();
                input.Image = MLImage.CreateFromFile(path);

                const int TrainingImageWidth = 800;
                const int TrainingImageHeight = 600;
                var output = _predict.Predict(input);

                CalculateAspectAndOffset(input.Image.Width, input.Image.Height, TrainingImageWidth, TrainingImageHeight, out float xOffset, out float yOffset, out float aspect);

                if (output.PredictedBoundingBoxes != null && output.PredictedBoundingBoxes.Length > 0)
                {
                    for (int x = 0; x < output.PredictedBoundingBoxes.Length; x += 2)
                    {
                        output.PredictedBoundingBoxes[x] = (output.PredictedBoundingBoxes[x] - xOffset) / aspect;
                        output.PredictedBoundingBoxes[x + 1] = (output.PredictedBoundingBoxes[x + 1] - yOffset) / aspect;
                    }
                }
                return output;
            });
        }

        private void CalculateAspectAndOffset(float sourceWidth, float sourceHeight, float destinationWidth, float destinationHeight, out float xOffset, out float yOffset, out float aspect)
        {
            float widthAspect = destinationWidth / sourceWidth;
            float heightAspect = destinationHeight / sourceHeight;
            xOffset = 0;
            yOffset = 0;
            if (heightAspect < widthAspect)
            {
                aspect = heightAspect;
                xOffset = (destinationWidth - (sourceWidth * aspect)) / 2;
            }
            else
            {
                aspect = widthAspect;
                yOffset = (destinationHeight - (sourceHeight * aspect)) / 2;
            }
        }
    }
}
