using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AJP.ElasticBand.API.Models;
using Force.DeepCloner;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AJP.ElasticBand.API
{
    public class AddCollectionsDocumentFilter : IDocumentFilter
    {
        private readonly IElasticBand _elasticBand;

        public AddCollectionsDocumentFilter(IElasticBand elasticBand)
        {
            _elasticBand = elasticBand;
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var collectionPathToCloneId = swaggerDoc.Paths["/Collection/{collectionName}/{id}"];
            var collectionPathToClone = swaggerDoc.Paths["/Collection/{collectionName}"];
            var collectionPathToCloneQuery = swaggerDoc.Paths["/Collection/{collectionName}/query"];

            var collectionDefinitions = GetCollections().Result;

            foreach (var collectionDefinition in collectionDefinitions)
            {
                var tag = new OpenApiTag
                {
                    Name = $"{collectionDefinition.Name}"
                };

                var deepClonedCollectionPathId = (OpenApiPathItem)collectionPathToCloneId.DeepClone();

                deepClonedCollectionPathId.Summary = $"summary for {collectionDefinition.Name}";
                deepClonedCollectionPathId.Description = $"description {collectionDefinition.Name}";

                AddTagsToOperations(deepClonedCollectionPathId.Operations.Values, tag);
                StripCollectionNameParamFromOperations(deepClonedCollectionPathId.Operations.Values);

                foreach (var operation in deepClonedCollectionPathId.Operations) 
                {
                    var idParam = FindParameterByName("id", operation.Value.Parameters);
                    if (idParam.Parameter != null)
                    {
                        idParam.Parameter.Example = new OpenApiString($"{collectionDefinition.ExampleIdPattern}");
                    }
                    
                    if (operation.Key == OperationType.Post)
                    {
                        operation.Value.RequestBody.Description = $"When posting to {collectionDefinition.Name} the json example will be pre=populated in the body for convenience. A timestamp property will be automatically added if it doesn't exist on the object. The id will also be added to the object automatically.";
                        foreach (var content in operation.Value.RequestBody.Content)
                        {
                            content.Value.Example = new OpenApiString($"{collectionDefinition.ExampleJsonObjectString}");
                        }
                    }
                }
                swaggerDoc.Paths.Add($"/Collection/{collectionDefinition.Name}/{{id}}", deepClonedCollectionPathId);


                var deepClonedCollectionPath = (OpenApiPathItem)collectionPathToClone.DeepClone();
                AddTagsToOperations(deepClonedCollectionPath.Operations.Values, tag);
                StripCollectionNameParamFromOperations(deepClonedCollectionPath.Operations.Values);
                swaggerDoc.Paths.Add($"/Collection/{collectionDefinition.Name}", deepClonedCollectionPath);


                var deepClonedCollectionPathQuery = (OpenApiPathItem)collectionPathToCloneQuery.DeepClone();
                AddTagsToOperations(deepClonedCollectionPathQuery.Operations.Values, tag);
                StripCollectionNameParamFromOperations(deepClonedCollectionPathQuery.Operations.Values);
                swaggerDoc.Paths.Add($"/Collection/{collectionDefinition.Name}/query", deepClonedCollectionPathQuery);

                // remove the original collection paths
                swaggerDoc.Paths.Remove("/Collection/{collectionName}/{id}");
                swaggerDoc.Paths.Remove("/Collection/{collectionName}");
                swaggerDoc.Paths.Remove("/Collection/{collectionName}/query");
            }                   
        }

        private async Task<List<CollectionDefinition>> GetCollections()
        {
            var response = await _elasticBand.Query<CollectionDefinition>(CollectionsIndex.Name, "").ConfigureAwait(false);

            if (response.Ok)
                return response.Data;

            return null;
        }

        private void AddTagsToOperations(ICollection<OpenApiOperation> operations, OpenApiTag tag) 
        {
            foreach (var operation in operations)
            {
                operation.Tags.Clear();
                operation.Tags.Add(tag);
            }
        }

        private void StripCollectionNameParamFromOperations(ICollection<OpenApiOperation> operations)
        {
            foreach (var operation in operations)
            {
                var collectionNameParam = FindParameterByName("collectionName", operation.Parameters);
                operation.Parameters.RemoveAt(collectionNameParam.Index);
            }
        }

        private (OpenApiParameter Parameter, int Index) FindParameterByName(string name, IList<OpenApiParameter> parameters)
        {
            var index = -1;
            foreach (var param in parameters)
            {
                if (param.Name == name)
                {
                    index = parameters.IndexOf(param);
                    return (param, index);
                }
            }
            return (null, index);
        }
    }
}
