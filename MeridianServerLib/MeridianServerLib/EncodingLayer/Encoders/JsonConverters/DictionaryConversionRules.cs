using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MeridianServerLib.EncodingLayer.Encoders.JsonConverters
{
    public class DictionaryConversionRules: JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Dictionary<byte, object>);
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            var result = new Dictionary<byte, object>();
            reader.Read();

            while (reader.TokenType == JsonToken.PropertyName)
            {
                var propertyName = Convert.ToByte(reader.Value);
                reader.Read();

                object value;

                if (reader.TokenType == JsonToken.Integer)
                {
                    value = Convert.ToInt32(reader.Value);
                }
                else if (reader.TokenType == JsonToken.Float)
                {
                    value = Convert.ToSingle(reader.Value);
                }
                else
                {
                    value = serializer.Deserialize(reader);
                }

                result.Add(propertyName, value);
                reader.Read();
            }

            return result;
        }
    }
}