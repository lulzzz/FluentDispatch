using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GrandCentralDispatch.Contract.Helpers;
using GrandCentralDispatch.Contract.Models;
using GrandCentralDispatch.Contract.Models.Tensorflow;
using MagicOnion;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Resolvers;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace GrandCentralDispatch.Contract.Resolvers
{
    public sealed class
        SentimentPredictionResolver : Item2RemotePartialResolver<MovieReview, MovieReviewSentimentPrediction>
    {
        private readonly ILogger _logger;

        public SentimentPredictionResolver(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SentimentPredictionResolver>();
        }

        /// <summary>
        /// Process each new movie review
        /// </summary>
        /// <param name="movieReview"><see cref="MovieReview"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <returns><see cref="UnaryResult{TResult}"/></returns>
        public override async UnaryResult<MovieReviewSentimentPrediction> ProcessItem2Remotely(MovieReview movieReview,
            NodeMetrics nodeMetrics)
        {
            _logger.LogInformation(
                $"Movie review received from node {nodeMetrics.Id}: {movieReview.ReviewText}.");
            var mlContext = new MLContext();
            var lookupMap = mlContext.Data.LoadFromTextFile(
                Path.Combine(Environment.CurrentDirectory, "sentiment_model", "imdb_word_index.csv"),
                columns: new[]
                {
                    new TextLoader.Column("Words", DataKind.String, 0),
                    new TextLoader.Column("Ids", DataKind.Int32, 1),
                },
                separatorChar: ','
            );

            void ResizeFeaturesAction(VariableLength s, FixedLength f)
            {
                var features = s.VariableLengthFeatures;
                Array.Resize(ref features, Constants.FeatureLength);
                f.Features = features;
            }

            var tensorFlowModel =
                mlContext.Model.LoadTensorFlowModel(Path.Combine(Environment.CurrentDirectory,
                    "sentiment_model"));
            var schema = tensorFlowModel.GetModelSchema();
            _logger.LogInformation(" =============== TensorFlow Model Schema =============== ");
            var featuresType = (VectorDataViewType) schema["Features"].Type;
            _logger.LogInformation(
                $"Name: Features, Type: {featuresType.ItemType.RawType}, Size: ({featuresType.Dimensions[0]})");
            var predictionType = (VectorDataViewType) schema["Prediction/Softmax"].Type;
            _logger.LogInformation(
                $"Name: Prediction/Softmax, Type: {predictionType.ItemType.RawType}, Size: ({predictionType.Dimensions[0]})");
            IEstimator<ITransformer> pipeline =
                mlContext.Transforms.Text.TokenizeIntoWords("TokenizedWords", "ReviewText")
                    .Append(mlContext.Transforms.Conversion.MapValue("VariableLengthFeatures", lookupMap,
                        lookupMap.Schema["Words"], lookupMap.Schema["Ids"], "TokenizedWords"))
                    .Append(mlContext.Transforms.CustomMapping(
                        (Action<VariableLength, FixedLength>) ResizeFeaturesAction, "Resize"))
                    .Append(tensorFlowModel.ScoreTensorFlowModel("Prediction/Softmax", "Features"))
                    .Append(mlContext.Transforms.CopyColumns("Prediction", "Prediction/Softmax"));
            var dataView = mlContext.Data.LoadFromEnumerable(new List<MovieReview>());
            var model = pipeline.Fit(dataView);
            var engine =
                mlContext.Model.CreatePredictionEngine<MovieReview, MovieReviewSentimentPrediction>(model);
            var sentimentPrediction = engine.Predict(movieReview);
            _logger.LogInformation("Number of classes: {0}", sentimentPrediction.Prediction.Length);
            _logger.LogInformation("Is sentiment/review positive? {0}",
                sentimentPrediction.Prediction[0] < 0.5 ? "Yes." : "No.");
            return await Task.FromResult(sentimentPrediction);
        }
    }
}