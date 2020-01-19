using System;
using System.Dynamic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AJP.ElasticBand.API.Models;

namespace AJP.ElasticBand.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CollectionsController : ControllerBase
    {
        private readonly ILogger<CollectionController> _logger;
        private readonly IElasticRepository<CollectionDefinition> _collectionRepository;

        public CollectionsController(ILogger<CollectionController> logger, IElasticRepository<CollectionDefinition> collectionRepository)
        {
            _logger = logger;
            _collectionRepository = collectionRepository;
        }

        /// <summary>
        /// Get all collections
        /// </summary>
        [HttpGet]
        public async Task<APIResponse> Get()
        {
            _logger.LogInformation($"get request recieved");

            var response = await _collectionRepository.Query("").ConfigureAwait(false);

            if (response.Ok)
            {
                _logger.LogInformation($"fetched all {response.Data.Count} collection(s)");
                return new APIResponse
                {
                    Request = Request.ToRequestString(),
                    Ok = true,
                    Result = "Found",
                    Data = response.Data
                };
            }

            _logger.LogInformation($"no collections to fetch");
            return APIResponse.NotOk(Request.ToRequestString(), "unable to fetch data to elasticsearch", HttpStatusCode.NoContent, response.Id);
        }

        /// <summary>
        /// Get a collection by id/name
        /// </summary>
        /// <param name="name">The name of the collection</param>
        [HttpGet("{name}")]
        public async Task<APIResponse> Get(string name)
        {
            _logger.LogInformation($"get request for id {name} recieved");

            var id = CollectionDefinition.BuildId(name);

            var response = await _collectionRepository.GetById(id).ConfigureAwait(false);

            response.Data.ExampleJsonObject = JsonSerializer.Deserialize<object>(response.Data.ExampleJsonObjectString);

            if (response.Ok)
            {
                _logger.LogInformation($"fetched collection {response.Id}");
                return new APIResponse
                {
                    Request = Request.ToRequestString(),
                    Ok = true,
                    Result = "Found",
                    Data = response.Data
                };
            }

            _logger.LogInformation($"unable to fetch collection( for {id}");
            return APIResponse.NotOk(Request.ToRequestString(), "unable to fetch data from elasticsearch", HttpStatusCode.NotFound, response.Id);
        }

        /// <summary>
        /// Post a new collection or update an existing collection.
        /// After creating/updating a collection, refresh the page to update SwaggerUI accordingly.
        /// </summary>
        /// <param name="name">The name of the collection to save or update</param>
        /// <param name="exampleIdPattern">An example Id string </param>
        /// <remarks>
        /// 
        /// Examples: 
        /// 
        /// The ExampleIdPattern will be prefilled into the id field (in Swagger) when posting instances of items in the collection.
        /// 
        /// It is only an example for convenience and can be changed to any string value when posting an item.
        /// 
        /// Users|[NewGuid] (when posting, with this IdPattern, the string '[NewGuid]' will be replaced by a new generated Guid)
        /// 
        /// Users|UserName (if you want predictable Ids etc)
        ///  
        /// </remarks>
        /// <param name="exampleJsonObject">Some json which will be prefilled into the request body (in Swagger) when posting instances of items in the collection.</param>
        [HttpPost]
        public async Task<APIResponse> Post(string name, string exampleIdPattern, [FromBody]ExpandoObject exampleJsonObject) 
        {
            _logger.LogInformation($"post request recieved");

            var collectionDefinition = new CollectionDefinition
            {
                Name = name.ToLower(),
                ExampleIdPattern = exampleIdPattern,
                ExampleJsonObjectString = JsonSerializer.Serialize<object>(exampleJsonObject, new JsonSerializerOptions { WriteIndented = true })
            };

            var response = await _collectionRepository.Index(collectionDefinition.Id, collectionDefinition).ConfigureAwait(false);

            if (response.Ok)
            {
                _logger.LogInformation($"posted collection {response.Result} {response.Id}"); 
                return new APIResponse
                {
                    Request = Request.ToRequestString(),
                    Ok = true,
                    Result = response.Result,
                    StatusCode = response.StatusCode,
                    Data = response.Id
                };
            }

            _logger.LogInformation($"unable to post collection to elasticsearch {response.StatusCode}");
            return APIResponse.NotOk(Request.ToRequestString(), "unable to post data to elasticsearch", HttpStatusCode.BadRequest, response.Id);
        }

        /// <summary>
        /// Delete a collection, all items in the collection will also be deleted.
        /// </summary>
        /// <param name="name">The name of the colection to delete.</param>
        [HttpDelete]
        public async Task<APIResponse> Delete(string name) 
        {
            var id = CollectionDefinition.BuildId(name);
            _logger.LogInformation($"delete request recieved for id {id}");
            
            var collectionResponse = await _collectionRepository.GetById(id);
            if (!collectionResponse.Ok)
            {
                return APIResponse.NotOk(Request.ToRequestString(), $"unable to delete data from elasticsearch, collection {name} not found", HttpStatusCode.NotFound);
            }
            await _collectionRepository.GetElasticBand().GetClient().DeleteAsync(collectionResponse.Data.Index);
            _logger.LogInformation($"deleted index {collectionResponse.Data.Index}");

            var deleteResponse = await _collectionRepository.Delete(id).ConfigureAwait(false);

            if (deleteResponse.Ok)
            {
                _logger.LogInformation($"deleted collection {name}");
                return new APIResponse
                {
                    Request = Request.ToRequestString(),
                    Ok = true,
                    Result = "Deleted",
                    Data = deleteResponse.Id
                };
            }

            _logger.LogInformation($"unable to delete collection with id {name} {deleteResponse.StatusCode}");
            return APIResponse.NotOk(Request.ToRequestString(), "unable to delete data from elasticsearch", HttpStatusCode.NotFound, deleteResponse.Id);

            // TODO could do with logging helper which creates a trace id and logs request and response data automatically?
            // or stores up messages and appends them all as one log per request??
        }
    }
}
