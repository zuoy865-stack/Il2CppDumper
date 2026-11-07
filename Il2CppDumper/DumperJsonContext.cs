using System.Text.Json.Serialization;

namespace Il2CppDumper
{
    [JsonSerializable(typeof(Config))]
    internal partial class ConfigJsonContext : JsonSerializerContext
    {
    }

    [JsonSourceGenerationOptions(WriteIndented = true, IncludeFields = true)]
    [JsonSerializable(typeof(ScriptJson))]
    [JsonSerializable(typeof(StringLiteralEntry[]))]
    internal partial class ScriptJsonContext : JsonSerializerContext
    {
    }

    internal sealed class StringLiteralEntry
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }
    }
}
