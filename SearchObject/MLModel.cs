using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.TorchSharp;
using Microsoft.ML.TorchSharp.AutoFormerV2;
using Microsoft.ML.Transforms.Image;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SearchObject
{
    public class MLModel
    {
        private const int TrainingImageWidth = 800;
        private const int TrainingImageHeight = 600;

        private PredictionEngine<ModelInput, ModelOutput> _predict;

        private readonly Common.Log _log;

        public MLModel(Common.Log log)
        {
            _log = log;
        }

        public Task LoadModelAsync(string path)
        {
            return Task.Run(() =>
            {
                _predict?.Dispose();

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

        public Task Train(string inputDataFilePath, string outputPath)
        {
            return Task.Run(() =>
            {
                var mlContext = new MLContext();
                mlContext.Log += MlContext_Log;
                var data = mlContext.Data.LoadFromEnumerable(LoadFromVott(inputDataFilePath));
                var model = RetrainModel(mlContext, data);

                // Pull the data schema from the IDataView used for training the model
                DataViewSchema dataViewSchema = data.Schema;
                using var fs = File.Create(outputPath);
                mlContext.Model.Save(model, dataViewSchema, fs);
            });
        }

        private void MlContext_Log(object? sender, LoggingEventArgs e)
        {
            _log.Write(e.Message);
        }

        private static ITransformer RetrainModel(MLContext mlContext, IDataView trainData)
        {
            var pipeline = BuildPipeline(mlContext);


            var model = pipeline.Fit(trainData);

            return model;
        }

        private static IEstimator<ITransformer> BuildPipeline(MLContext mlContext)
        {
            // Data process configuration with pipeline data transformations
            var pipeline = mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: @"Labels", inputColumnName: @"Labels", addKeyValueAnnotationsAsText: false)
                                    .Append(mlContext.Transforms.ResizeImages(outputColumnName: @"Image", inputColumnName: @"Image", imageHeight: TrainingImageHeight, imageWidth: TrainingImageWidth, cropAnchor: ImageResizingEstimator.Anchor.Center, resizing: ImageResizingEstimator.ResizingKind.IsoPad))
                                    .Append(mlContext.MulticlassClassification.Trainers.ObjectDetection(new ObjectDetectionTrainer.Options() { LabelColumnName = @"Labels", PredictedLabelColumnName = @"PredictedLabel", BoundingBoxColumnName = @"Box", ImageColumnName = @"Image", ScoreColumnName = @"score", MaxEpoch = 5, InitLearningRate = 1, WeightDecay = 0, }))
                                    .Append(mlContext.Transforms.Conversion.MapKeyToValue(outputColumnName: @"PredictedLabel", inputColumnName: @"PredictedLabel"));
            return pipeline;
        }

        private static void CalculateAspectAndOffset(float sourceWidth, float sourceHeight, float destinationWidth, float destinationHeight, out float xOffset, out float yOffset, out float aspect)
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

        private static IEnumerable<ModelInput> LoadFromVott(string inputDataFilePath)
        {
            JsonNode jsonNode;
            using (StreamReader r = new StreamReader(inputDataFilePath))
            {
                string json = r.ReadToEnd();
                jsonNode = JsonSerializer.Deserialize<JsonNode>(json)!;
            }

            var imageData = new List<ModelInput>();
            foreach (KeyValuePair<string, JsonNode> asset in jsonNode["assets"].AsObject())
            {
                var labelList = new List<string>();
                var boxList = new List<float>();

                var sourceWidth = asset.Value["asset"]["size"]["width"].GetValue<float>();
                var sourceHeight = asset.Value["asset"]["size"]["height"].GetValue<float>();

                CalculateAspectAndOffset(sourceWidth, sourceHeight, TrainingImageWidth, TrainingImageHeight, out float xOffset, out float yOffset, out float aspect);

                foreach (var region in asset.Value["regions"].AsArray())
                {
                    foreach (var tag in region["tags"].AsArray())
                    {
                        labelList.Add(tag.GetValue<string>());
                        var boundingBox = region["boundingBox"];
                        var left = boundingBox["left"].GetValue<float>();
                        var top = boundingBox["top"].GetValue<float>();
                        var width = boundingBox["width"].GetValue<float>();
                        var height = boundingBox["height"].GetValue<float>();

                        boxList.Add(xOffset + (left * aspect));
                        boxList.Add(yOffset + (top * aspect));
                        boxList.Add(xOffset + ((left + width) * aspect));
                        boxList.Add(yOffset + ((top + height) * aspect));
                    }

                }

                var mlImage = MLImage.CreateFromFile(asset.Value["asset"]["path"].GetValue<string>().Replace("file:", ""));
                var modelInput = new ModelInput()
                {
                    Image = mlImage,
                    Labels = labelList.ToArray(),
                    Box = boxList.ToArray(),
                };

                imageData.Add(modelInput);
            }

            return imageData;
        }
    }
}
