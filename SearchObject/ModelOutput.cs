using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchObject
{
    /// <summary>
    /// model output class for MLModel1.
    /// </summary>
    public class ModelOutput
    {
        [ColumnName(@"Labels")]
        public uint[] Labels { get; set; }

        [ColumnName(@"Image")]
        [Microsoft.ML.Transforms.Image.ImageType(600, 800)]
        public MLImage Image { get; set; }

        [ColumnName(@"Box")]
        public float[] Box { get; set; }

        [ColumnName(@"PredictedLabel")]
        public string[] PredictedLabel { get; set; }

        [ColumnName(@"score")]
        public float[] Score { get; set; }

        [ColumnName(@"PredictedBoundingBoxes")]
        public float[] PredictedBoundingBoxes { get; set; }

    }
}
