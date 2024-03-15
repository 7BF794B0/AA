namespace EventSchemaRegistry
{
    public class SchemaRegistry<T>
    {
        private NJsonSchema.JsonSchema _schema;
        private string _jsonSchema;

        public SchemaRegistry()
        {
            _schema = NJsonSchema.JsonSchema.FromType<T>();
            _jsonSchema = _schema.ToJson();
        }

        public bool ValidateSchema(string jsonData)
        {
            var errors = _schema.Validate(jsonData);

            if (errors.Count == 0)
                return true;
            else
                return false;
        }
    }
}
