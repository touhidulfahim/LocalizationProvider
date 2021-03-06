// Copyright (c) Valdis Iljuconoks. All rights reserved.
// Licensed under Apache-2.0. See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using DbLocalizationProvider.Sync;

namespace DbLocalizationProvider.Internal
{
    internal static class ResourceKeyBuilder
    {
        internal static string BuildResourceKey(string prefix, string name, string separator = ".")
        {
            return string.IsNullOrEmpty(prefix) ? name : prefix.JoinNonEmpty(separator, name);
        }

        internal static string BuildResourceKey(Type containerType, Stack<string> keyStack)
        {
            return BuildResourceKey(containerType, keyStack.Aggregate(string.Empty, (prefix, name) => BuildResourceKey(prefix, name)));
        }

        internal static string BuildResourceKey(string keyPrefix, Attribute attribute)
        {
            if(attribute == null) throw new ArgumentNullException(nameof(attribute));

            var result = BuildResourceKey(keyPrefix, attribute.GetType());
            if(attribute.GetType().IsAssignableFrom(typeof(DataTypeAttribute)))
            {
                result += ((DataTypeAttribute)attribute).DataType;
            }

            return result;
        }

        internal static string BuildResourceKey(string keyPrefix, Type attributeType)
        {
            if(attributeType == null) throw new ArgumentNullException(nameof(attributeType));
            if(!typeof(Attribute).IsAssignableFrom(attributeType)) throw new ArgumentException($"Given type `{attributeType.FullName}` is not of type `System.Attribute`");

            return $"{keyPrefix}-{attributeType.Name.Replace("Attribute", string.Empty)}";
        }

        internal static string BuildResourceKey(Type containerType, string memberName, Type attributeType)
        {
            return BuildResourceKey(BuildResourceKey(containerType, memberName), attributeType);
        }

        internal static string BuildResourceKey(Type containerType, string memberName, Attribute attribute)
        {
            return BuildResourceKey(BuildResourceKey(containerType, memberName), attribute);
        }

        internal static string BuildResourceKey(Type containerType, string memberName, string separator = ".")
        {
            var modelAttribute = containerType.GetCustomAttribute<LocalizedModelAttribute>();
            var mi = containerType.GetMember(memberName).FirstOrDefault();

            var prefix = string.Empty;

            if (!string.IsNullOrEmpty(modelAttribute?.KeyPrefix)) prefix = modelAttribute.KeyPrefix;

            var resourceAttributeOnClass = containerType.GetCustomAttribute<LocalizedResourceAttribute>();
            if (!string.IsNullOrEmpty(resourceAttributeOnClass?.KeyPrefix)) prefix = resourceAttributeOnClass.KeyPrefix;

            if(mi != null)
            {
                var resourceKeyAttribute = mi.GetCustomAttribute<ResourceKeyAttribute>();
                if(resourceKeyAttribute != null) return prefix.JoinNonEmpty(string.Empty, resourceKeyAttribute.Key);
            }

            if (!string.IsNullOrEmpty(prefix)) return prefix.JoinNonEmpty(separator, memberName);

            // ##### we need to understand where to look for the property
            var potentialResourceKey = containerType.FullName.JoinNonEmpty(separator, memberName);

            // 1. maybe property has [UseResource] attribute, if so - then we need to look for "redirects"
            if (TypeDiscoveryHelper.UseResourceAttributeCache.TryGetValue(potentialResourceKey, out var redirectedResourceKey)) return redirectedResourceKey;

            // 2. verify that property is declared on given container type
            if(modelAttribute == null || modelAttribute.Inherited) return potentialResourceKey;

            // 3. if not - then we scan through discovered and cached properties during initial scanning process and try to find on which type that property is declared
            var declaringTypeName = FindPropertyDeclaringTypeName(containerType, memberName);

            return declaringTypeName != null
                       ? declaringTypeName.JoinNonEmpty(separator, memberName)
                       : potentialResourceKey;
        }

        internal static string BuildResourceKey(Type containerType)
        {
            var modelAttribute = containerType.GetCustomAttribute<LocalizedModelAttribute>();
            var resourceAttribute = containerType.GetCustomAttribute<LocalizedResourceAttribute>();

            if(modelAttribute == null && resourceAttribute == null) throw new ArgumentException($"Type `{containerType.FullName}` is not decorated with localizable attributes ([LocalizedModelAttribute] or [LocalizedResourceAttribute])", nameof(containerType));

            return containerType.FullName;
        }

        private static string FindPropertyDeclaringTypeName(Type containerType, string memberName)
        {
            // make private copy
            var currentContainerType = containerType;

            while (true)
            {
                if(currentContainerType == null) return null;

                var fullName = currentContainerType.FullName;
                if(currentContainerType.IsGenericType && !currentContainerType.IsGenericTypeDefinition) fullName = currentContainerType.GetGenericTypeDefinition().FullName;

                if(TypeDiscoveryHelper.DiscoveredResourceCache.TryGetValue(fullName, out var properties))
                {
                    // property was found in the container
                    if(properties.Contains(memberName)) return fullName;
                }

                currentContainerType = currentContainerType.BaseType;
            }
        }
    }
}
