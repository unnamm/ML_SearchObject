using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchObject
{
    /// <summary>
    /// model input class for MLModel1.
    /// </summary>
    public partial class ModelInput
    {
        [LoadColumn(0)]
        [ColumnName(@"Labels")]
        public string[] Labels { get; set; }

        [LoadColumn(1)]
        [ColumnName(@"Image")]
        [Microsoft.ML.Transforms.Image.ImageType(800, 600)]
        public MLImage Image { get; set; }

        [LoadColumn(2)]
        [ColumnName(@"Box")]
        public float[] Box { get; set; }
    }
}