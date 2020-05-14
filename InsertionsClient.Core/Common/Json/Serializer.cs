// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Microsoft.Net.Insertions.Common.Json
{
    /// <summary>
    /// Contains JSon serializing utilities.
    /// </summary>
    public static class Serializer
    {
        private static readonly List<Type> DataContractCustomTypes = new List<Type>();


        public static void AddDataContractCustomType(Type type)
        {
            _ = type ?? throw new ArgumentNullException(paramName: nameof(type));
            if (!DataContractCustomTypes.Contains(type))
            {
                DataContractCustomTypes.Add(type);
            }
        }

        /// <summary>
        /// Deserializes a specified json to a targeted type <typeparamref name="TModel"/>.
        /// </summary>
        /// <typeparam name="TModel">Targeted type.</typeparam>
        /// <param name="json">Specified json string.</param>
        /// <returns><typeparamref name="TModel"/> instance.</returns>
        public static TModel Deserialize<TModel>(string json)
        {
            return Deserialize<TModel>(json, DataContractCustomTypes);
        }

        /// <summary>
        /// Deserializes a specified json to a targeted type <typeparamref name="TModel"/>.
        /// </summary>
        /// <typeparam name="TModel">Targeted type.</typeparam>
        /// <param name="json">Specified json string.</param>
        /// <param name="types"><see cref="IEnumerable{Type}"/> of types to familiarize the serializer with.</param>
        /// <returns><typeparamref name="TModel"/> instance.</returns>
        public static TModel Deserialize<TModel>(string json, IEnumerable<Type> types)
        {
            _ = string.IsNullOrWhiteSpace(json) ? throw new ArgumentNullException(paramName: nameof(json)) : json;
            using MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(json));
            DataContractJsonSerializer serializer = types == null ?
                new DataContractJsonSerializer(typeof(TModel)) :
                new DataContractJsonSerializer(typeof(TModel), types);

            return (TModel)serializer.ReadObject(stream);
        }

        /// <summary>
        /// Serializes the specified object to a JSon string.
        /// </summary>
        /// <typeparam name="TModel">Type of object to serialize</typeparam>
        /// <param name="instance"></param>
        /// <returns>Corresponding JSon string.</returns>
        public static string Serialize<TModel>(TModel instance)
        {
            return Serialize(instance, new DataContractJsonSerializer(typeof(TModel), DataContractCustomTypes));
        }

        private static string Serialize<TModel>(TModel instance, DataContractJsonSerializer serializer)
        {
            _ = instance ?? throw new ArgumentNullException(paramName: nameof(instance));
            using MemoryStream stream = new MemoryStream();
            serializer.WriteObject(stream, instance);
            stream.Position = 0;
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}