using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using AJP.ElasticBand.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AJP.ElasticBand.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CollectionController : ControllerBase
    {
        private readonly ILogger<CollectionController> _logger;
        private readonly IElasticBand _elasticBand;
        private readonly IElasticRepository<CollectionDefinition> _collectionRepository;

        public CollectionController(ILogger<CollectionController> logger, IElasticBand elasticBand, IElasticRepository<CollectionDefinition> collectionRepository)
        {
            _logger = logger;
            _elasticBand = elasticBand;
            _collectionRepository = collectionRepository;
        }

        /// <summary>
        /// Post a new item or update an existing item in the collection.
        /// </summary>
        /// <param name="collectionName">The name of the collection to post to</param>
        /// <param name="id">The ID of the item to post or update, to add a new Guid, include the string [NewGuid].</param>
        /// <param name="payload">Json containing the data to be saved or updated.</param>
        [HttpPost("{collectionName}/{id}")]
        public async Task<APIResponse> PostRecordToCollection(string collectionName, string id, [FromBody]ExpandoObject payload)
        {
            _logger.LogInformation($"PostRecordToCollection request recieved");

            id = id.Replace("[NewGuid]", Guid.NewGuid().ToString());

            var payloadDictionary = (IDictionary<String, object>)payload;
            if (!payloadDictionary.ContainsKey("timestamp"))
            {
                payloadDictionary.Add("timestamp", DateTime.Now);
            }

            if (payloadDictionary.ContainsKey("id"))
                payloadDictionary["id"] = id;
            else
                payloadDictionary.Add("id", id);
            

            var collection = await GetCollection(collectionName).ConfigureAwait(false);
            if (collection == null)
                return APIResponse.NotOk(Request.ToRequestString(), $"Cant find collection with name {collectionName}", HttpStatusCode.BadRequest);

            var response = await _elasticBand.Index<object>(collection.Index, payload, id).ConfigureAwait(false);
            if (response.Ok)
                return new APIResponse 
                {
                    Request = Request.ToRequestString(),
                    Ok = true,
                    Result = response.Result,
                    StatusCode = response.StatusCode,
                    Data = $"{collection.Index}/_doc/{response.Id}"
                };

            return APIResponse.NotOk(Request.ToRequestString(), "Failed to index data", HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// Get all items from the collection.
        /// </summary>
        /// <param name="collectionName">The name of the collection to get items from.</param>
        [HttpGet("{collectionName}")]
        public async Task<APIResponse> GetAllRecords(string collectionName) 
        {
            _logger.LogInformation($"GetAllRecords request recieved");

            var collection = await GetCollection(collectionName).ConfigureAwait(false);
            if (collection == null)
                return APIResponse.NotOk(Request.ToRequestString(), $"Cant find collection with name {collectionName}", HttpStatusCode.BadRequest);

            var response = await _elasticBand.Query<object>(collection.Index, "").ConfigureAwait(false);
            if (response.Ok)
                return new APIResponse
                {
                    Request = Request.ToRequestString(),
                    Ok = true,
                    Result = "found",
                    Data = response.Data
                };

            return APIResponse.NotOk(Request.ToRequestString(), "No data or index doesn't exist", HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Get an item from the collection.
        /// </summary>
        /// <param name="collectionName">The name of the collection to get an item from.</param>
        /// <param name="id">The id of the item to get.</param>
        [HttpGet("{collectionName}/{id}")]
        public async Task<APIResponse> GetRecordById(string collectionName, string id)
        {
            _logger.LogInformation($"GetRecordById request recieved");

            var collection = await GetCollection(collectionName).ConfigureAwait(false);
            if (collection == null)
                return APIResponse.NotOk(Request.ToRequestString(), $"Cant find collection with name {collectionName}", HttpStatusCode.BadRequest);

            var response = await _elasticBand.GetDocumentByID<object>(collection.Index, id).ConfigureAwait(false);
            if (response.Ok)
                return new APIResponse
                {
                    Request = Request.ToRequestString(),
                    Ok = true,
                    Result = "found",
                    Data = response.Data
                };

            return APIResponse.NotOk(Request.ToRequestString(), "Failed to return any data", HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Find items by query string.
        /// </summary>
        /// <param name="collectionName">The name of the collection to delete an item from.</param>
        /// <param name="searchString">A string containing a simple search query, overriden by the query in the body if present.</param>
        /// <param name="query">A string containing valid Elasticsearch Json query syntax, leave as {} to use the searchString instead.</param>
        /// <remarks>
        /// ## Example query strings:
        /// 
        /// 'andrew' (searches whole documents for the string 'andrew' optionally use * as wildcard)
        /// 
        /// 'age > 10' (simple single greater than or less than query, doesn't work with ' and ')
        /// 
        /// 'email:gmail*' (searches document property named 'email' for the strings starting with 'gmail')
        /// 
        /// 'email:gmail* and notes:blah' (combine query clauses with ' and ')
        /// 
        /// ## Example json query 
        /// 
        /// This query must be valid Elasticsearch Json query syntax, you can actually get this from Kibana using Inspect->Response
        /// 
        /// the following query finds items where a numeric property named age is greater than 38:
        /// 
        ///         {
        ///           "version": true,
        ///           "size": 500,
        ///           "query": {
        ///             "bool": {
        ///               "filter": [
        ///                 {
        ///                   "bool": {
        ///                     "should": [
        ///                       {
        ///                         "range": {
        ///                           "age": {
        ///                             "gt": 38
        ///                           }
        ///                         }
        ///                       }
        ///                     ],
        ///                     "minimum_should_match": 1
        ///                   }
        ///                 }
        ///               ]
        ///             }
        ///           }
        ///         }        
        /// 
        /// </remarks>
        [HttpPost("{collectionName}/query")]
        public async Task<APIResponse> GetRecordByQuery(string collectionName, string searchString, [FromBody]object query)
        {
            _logger.LogInformation($"GetRecordByQuery request recieved");

            var collection = await GetCollection(collectionName).ConfigureAwait(false);
            if (collection == null)
                return APIResponse.NotOk(Request.ToRequestString(), $"Cant find collection with name {collectionName}", HttpStatusCode.BadRequest);

            // json query from body should override search string.
            var queryAsString = JsonSerializer.Serialize(query);
            if (queryAsString != "{}")
                searchString = queryAsString;

            var response = await _elasticBand.Query<object>(collection.Index, searchString).ConfigureAwait(false);
            if (response.Ok)
                return new APIResponse
                {
                    Request = Request.ToRequestString(),
                    Ok = true,
                    Result = response.Data.Count == 0 ? "not found": "found",
                    Data = response.Data
                };

            return APIResponse.NotOk(Request.ToRequestString(), "Failed to return any data", HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Delete an item from the collection.
        /// </summary>
        /// <param name="collectionName">The name of the collection to delete an item from.</param>
        /// <param name="id">The id of the item to delete.</param>
        [HttpDelete("{collectionName}/{id}")]
        public async Task<APIResponse> DeleteRecordById(string collectionName, string id)
        {
            _logger.LogInformation($"DeleteRecordById request recieved");

            var collection = await GetCollection(collectionName).ConfigureAwait(false);
            if (collection == null)
                return APIResponse.NotOk(Request.ToRequestString(), $"Cant find collection with name {collectionName}", HttpStatusCode.BadRequest);

            var response = await _elasticBand.Delete(collection.Index, id).ConfigureAwait(false);
            if (response.Ok)
                return new APIResponse
                {
                    Request = Request.ToRequestString(),
                    Ok = true,
                    Result = "deleted",
                    Data = id
                };

            return APIResponse.NotOk(Request.ToRequestString(), "Failed to delete record", HttpStatusCode.NotFound);
        }

        private async Task<CollectionDefinition> GetCollection(string collectionName)
        {
            var id = CollectionDefinition.BuildId(collectionName);
            var response = await _collectionRepository.GetById(id).ConfigureAwait(false);

            if (response.Ok)
                return response.Data;

            return null;
        }
    }
}
