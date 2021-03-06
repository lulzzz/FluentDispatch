﻿using MessagePack;
using Microsoft.ML.Data;

namespace FluentDispatch.Contract.Models
{
    /// <summary>
    /// This is the payload exchanged between the cluster and its nodes
    /// It should be decorated by <see cref="MessagePackObjectAttribute"/> because it is serialized using MessagePack
    /// </summary>
    [MessagePackObject(true)]
    public class MovieReviewSentimentPrediction
    {
        [VectorType(2)]
        public float[] Prediction { get; set; }
    }
}
