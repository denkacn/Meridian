using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MeridianRequestSystem.RequestSystem.Attributes;
using MeridianRequestSystem.RequestSystem.Exceptions;
using MeridianRequestSystem.RequestSystem.Interfaces;
using MeridianRequestSystem.RequestSystem.Requests.PackingProviders;

namespace MeridianRequestSystem.RequestSystem.Helpers
{
    public static class RequestMapper
    {
        private static readonly ConcurrentDictionary<Type, MemberBinding[]> BindingCache =
            new ConcurrentDictionary<Type, MemberBinding[]>();

        public static RequestDataPack GetRequestData(IDataNetworkRequest request)
        {
            var requestBaseAttributeData = GetAttributeData(request.GetType());
            if (requestBaseAttributeData == null)
            {
                throw new NotFoundRequestAttributeException("Not Found Request Attribute On " + request.GetType());
            }

            var requestData = GetFieldAttributeData(request);

            return new RequestDataPack(requestBaseAttributeData.CommandId, requestBaseAttributeData.IsNecessarily,
                requestData);
        }

        private static RequestBaseAttributeData GetAttributeData(Type t)
        {
            var attributeData = (RequestBaseAttribute)Attribute.GetCustomAttribute(t, typeof(RequestBaseAttribute));

            if (attributeData != null)
            {
                var requestBaseAttributeData = new RequestBaseAttributeData(attributeData.CommandId,
                    attributeData.IsNecessarily);

                return requestBaseAttributeData;
            }

            return null;
        }

        public static Dictionary<byte, object> GetFieldAttributeData(object obj)
        {
            var data = new Dictionary<byte, object>();

            foreach (var binding in GetBindings(obj.GetType()))
            {
                object value;

                if (binding.Attribute.PackingProviderType != null)
                {
                    var packingProvider =
                        (IFieldPackingProvider)Activator.CreateInstance(binding.Attribute.PackingProviderType);

                    value = packingProvider.To(binding.GetValue(obj));
                }
                else
                {
                    value = binding.GetValue(obj);
                }

                data[binding.Attribute.IndexId] = value;
            }

            return data;
        }

        public static void AutoMap(object obj, IReadOnlyDictionary<byte, object> package)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (package == null) throw new ArgumentNullException(nameof(package));

            foreach (var binding in GetBindings(obj.GetType()))
            {
                var id = binding.Attribute.IndexId;
                if (!package.TryGetValue(id, out var rawValue))
                {
                    continue;
                }

                var value = DecodeValue(binding, rawValue);
                binding.SetValue(obj, value);
            }
        }

        private static object DecodeValue(MemberBinding binding, object rawValue)
        {
            if (binding.Attribute.PackingProviderType != null)
            {
                var packingProvider =
                    (IFieldPackingProvider)Activator.CreateInstance(binding.Attribute.PackingProviderType);

                return packingProvider.From(rawValue);
            }

            if (rawValue == null) return null;

            var targetType = Nullable.GetUnderlyingType(binding.MemberType) ?? binding.MemberType;

            if (targetType == typeof(byte[]) && rawValue is string base64)
            {
                return Convert.FromBase64String(base64);
            }

            if (targetType.IsInstanceOfType(rawValue))
            {
                return rawValue;
            }

            if (targetType.IsEnum)
            {
                return Enum.ToObject(targetType, rawValue);
            }

            return Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
        }

        private static MemberBinding[] GetBindings(Type type)
        {
            return BindingCache.GetOrAdd(type, CreateBindings);
        }

        private static MemberBinding[] CreateBindings(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var fieldBindings = type
                .GetFields(flags)
                .Select(field => new
                {
                    Member = (MemberInfo)field,
                    Attribute = field.GetCustomAttribute<RequestFieldAttribute>(true),
                    Type = field.FieldType
                });

            var propertyBindings = type
                .GetProperties(flags)
                .Where(property => property.GetIndexParameters().Length == 0 && property.GetSetMethod(true) != null)
                .Select(property => new
                {
                    Member = (MemberInfo)property,
                    Attribute = property.GetCustomAttribute<RequestFieldAttribute>(true),
                    Type = property.PropertyType
                });

            return fieldBindings
                .Concat(propertyBindings)
                .Where(binding => binding.Attribute != null)
                .Select(binding => new MemberBinding(binding.Member, binding.Attribute, binding.Type))
                .ToArray();
        }

        public class RequestBaseAttributeData
        {
            public readonly byte CommandId;
            public readonly bool IsNecessarily;

            public RequestBaseAttributeData(byte commandId, bool isNecessarily)
            {
                CommandId = commandId;
                IsNecessarily = isNecessarily;
            }
        }

        private sealed class MemberBinding
        {
            public readonly MemberInfo Member;
            public readonly RequestFieldAttribute Attribute;
            public readonly Type MemberType;

            public MemberBinding(MemberInfo member, RequestFieldAttribute attribute, Type memberType)
            {
                Member = member;
                Attribute = attribute;
                MemberType = memberType;
            }

            public object GetValue(object obj)
            {
                if (Member is FieldInfo field) return field.GetValue(obj);
                if (Member is PropertyInfo property) return property.GetValue(obj);
                throw new InvalidOperationException("Unsupported request member type.");
            }

            public void SetValue(object obj, object value)
            {
                if (Member is FieldInfo field)
                {
                    field.SetValue(obj, value);
                    return;
                }

                if (Member is PropertyInfo property)
                {
                    property.SetValue(obj, value);
                    return;
                }

                throw new InvalidOperationException("Unsupported request member type.");
            }
        }
    }

    public class RequestDataPack
    {
        public readonly byte CommandId;
        public readonly bool IsNecessarily;
        public readonly Dictionary<byte, object> Request;

        public RequestDataPack(byte commandId, bool isNecessarily, Dictionary<byte, object> request)
        {
            CommandId = commandId;
            IsNecessarily = isNecessarily;
            Request = request;
        }
    }
}
