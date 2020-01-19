namespace AJP.ElasticBand.API
{
    public class CollectionDefinitionRepository<T> : ElasticRepository<T>
    {

        public CollectionDefinitionRepository(IElasticBand elasticBand) : base(CollectionsIndex.Name, elasticBand)
        {
        }
    }

    public static class CollectionsIndex 
    {
        public const string Name = "eb_collections";    
    }
}
