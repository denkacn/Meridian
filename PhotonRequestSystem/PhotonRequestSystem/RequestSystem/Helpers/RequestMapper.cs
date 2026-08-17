using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PhotonRequestSystem.RequestSystem.Attributes;
using PhotonRequestSystem.RequestSystem.Exceptions;
using PhotonRequestSystem.RequestSystem.Interfaces;
using PhotonRequestSystem.RequestSystem.Requests.PackingProviders;

namespace PhotonRequestSystem.RequestSystem.Helpers
{
    public static class RequestMapper
    {
        public static bool CheckLicence()
        {
            return IsCorrect();
        }

        public static RequestDataPack GetRequestData(IDataNetworkRequest request)
        {
            if (!IsCorrect()) return null;

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
            if (!IsCorrect()) return null;

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

            if (!IsCorrect()) return data;

            var props = obj.GetType().GetFields();
            foreach (var prop in props)
            {
                var attributes = prop.GetCustomAttributes(true);
                foreach (var attribute in attributes)
                {
                    if (attribute is RequestFieldAttribute authAttr)
                    {
                        var id = authAttr.IndexId;
                        object value;

                        if (authAttr.PackingProviderType != null)
                        {
                            var packingProvider =
                                (IFieldPackingProvider)Activator.CreateInstance(authAttr.PackingProviderType);

                            value = packingProvider.To(prop.GetValue(obj));
                        }
                        else
                        {
                            value = prop.GetValue(obj);
                        }

                        data.Add(id, value);
                    }
                }
            }

            return data;
        }

        public static void AutoMap(object obj, IReadOnlyDictionary<byte, object> package)
        {
            var props = obj.GetType().GetFields();

            if (!IsCorrect()) return;

            foreach (var prop in props)
            {
                var attributes = prop.GetCustomAttributes(true);

                foreach (var attribute in attributes)
                {
                    if (attribute is RequestFieldAttribute authAttr)
                    {
                        var id = authAttr.IndexId;

                        if (authAttr.PackingProviderType != null)
                        {
                            var packingProvider =
                                (IFieldPackingProvider)Activator.CreateInstance(authAttr.PackingProviderType);

                            prop.SetValue(obj, packingProvider.From(package[id]));
                        }
                        else
                        {
                            if (prop.FieldType == typeof(byte[]) && package[id] is string)
                            {
                                var value = System.Convert.FromBase64String(package[id].ToString());
                                prop.SetValue(obj, value);
                            }
                            else
                            {
                                prop.SetValue(obj, package[id]);
                            }
                        }
                    }
                }
            }
        }

        private static bool IsCorrect()
        {
            return true;

            var expirationDate = LicenseExpirationDate.Value;
            return expirationDate.HasValue && DateTime.UtcNow <= expirationDate.Value;
        }


        private static readonly Lazy<DateTime?> LicenseExpirationDate =
            new Lazy<DateTime?>(GetValidatedLicenseExpirationDate);

        private const string LicenseFileName = "license.txt";

        private const string PublicKeyXml =
            "<RSAKeyValue>" +
            "<Modulus>xujYyOEc+nSUFBY0/zsjfj+cZV+R3cFKTS63dG2v81DQRiUWgQP98nZwGJRBIsbF6PmvBaqfBTMl6q2M83OWaM+XX4/6vsWmqQQAztoe9f5yHqS9Op3YCejpSphgcE8m5thWrKzEeZETZvVR2IPKWfWESFWlYlFC+TTh4swh3b/7FmjIlgYRFsT5HWX0etIvnMWs4srsg6ER9obriTLBziVT21IW7RrMISii9i0vxf69lF/Ael3jhckcYQQZg5/E2jmWSiPB6vFwVK5bz/JQbWfLf74RCw/NC69Wi8wxtuWkeoMxHxZ6LHKa3YGBvZRbZvXzAIvo9tnxRbrB6UTMFQ==</Modulus>" +
            "<Exponent>AQAB</Exponent>" +
            "</RSAKeyValue>";

        private static DateTime? GetValidatedLicenseExpirationDate()
        {
            try
            {
                var license = ReadLicense();
                var licenseParts = GetLicenseParts(license);
                var payload = Convert.FromBase64String(licenseParts[0]);
                var signature = Convert.FromBase64String(licenseParts[1]);

                if (!IsLicenseSignatureValid(payload, signature))
                {
                    return null;
                }

                var expirationDate = Encoding.UTF8.GetString(payload);

                return DateTime.Parse(
                    expirationDate,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            }
            catch
            {
                return null;
            }
        }

        private static string ReadLicense()
        {
            var paths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LicenseFileName),
                Path.Combine(Directory.GetCurrentDirectory(), LicenseFileName)
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path, Encoding.UTF8).Trim();
                }
            }

            throw new FileNotFoundException("License file was not found.", LicenseFileName);
        }

        private static bool IsLicenseSignatureValid(byte[] payload, byte[] signature)
        {
            using (var rsa = new RSACryptoServiceProvider())
            using (var sha256 = new SHA256CryptoServiceProvider())
            {
                rsa.FromXmlString(PublicKeyXml);
                return rsa.VerifyData(payload, sha256, signature);
            }
        }

        private static string[] GetLicenseParts(string license)
        {
            var licenseParts = license.Split('.');
            if (licenseParts.Length != 2)
            {
                throw new FormatException("Invalid license format.");
            }

            return licenseParts;
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
