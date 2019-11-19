using FluentDispatch.Contract.Helpers;
using Microsoft.ML.Data;

namespace FluentDispatch.Contract.Models.Tensorflow
{
    public class FixedLength
    {
        /// <summary>
        /// This is a fixed length vector designated by VectorType attribute.
        /// </summary>
        [VectorType(Constants.FeatureLength)]
        public int[] Features { get; set; }
    }
}
