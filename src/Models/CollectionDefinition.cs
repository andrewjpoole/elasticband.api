using System;

namespace AJP.ElasticBand.API.Models
{
    public class CollectionDefinition
    {
        public string Name { get; set; }
        public string ExampleJsonObjectString { get; set; }
        public object ExampleJsonObject { get; set; }
        public string ExampleIdPattern { get; set; }
        public DateTime Timestamp { get; set; }
        public string EBDataType => "Collection";
        public string Index => $"eb_collections_{Name}";
        public string Id => $"{IdPrefix}{Name}";

        public const string IdPrefix = "CollectionDefinitions|";
        public static string BuildId(string name) => name.StartsWith(IdPrefix) ? name : $"{IdPrefix}{name}";

        public CollectionDefinition()
        {
            Timestamp = DateTime.UtcNow;
        }
    }
}
