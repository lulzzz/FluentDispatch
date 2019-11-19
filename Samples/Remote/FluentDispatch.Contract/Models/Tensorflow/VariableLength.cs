using Microsoft.ML.Data;

namespace FluentDispatch.Contract.Models.Tensorflow
{
    public class VariableLength
    {
        /// <summary>
        /// This is a variable length vector designated by VectorType attribute.
        /// Variable length vectors are produced by applying operations such as 'TokenizeWords' on strings
        /// resulting in vectors of tokens of variable lengths.
        /// </summary>
        [VectorType]
        public int[] VariableLengthFeatures { get; set; }
    }
}
