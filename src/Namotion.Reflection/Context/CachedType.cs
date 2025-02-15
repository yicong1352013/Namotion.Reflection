﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Namotion.Reflection
{
    /// <summary>
    /// A cached type object without context.
    /// </summary>
    public class CachedType
    {
        /// <summary>
        /// Clears the cache.
        /// </summary>
        public static void ClearCache()
        {
            ContextualTypeExtensions.ClearCache();
        }

        private Type? _type;
        private bool _isNullableType;
        private string? _typeName;

        private Attribute[]? _inheritedAttributes;

        /// <summary>
        /// Internal generic arguments.
        /// </summary>
        internal object? _genericArguments;

        /// <summary>
        /// Internal original generic arguments.
        /// </summary>
        internal object? _originalGenericArguments;

        /// <summary>
        /// Internal element type.
        /// </summary>
        internal object? _elementType;

        /// <summary>
        /// Unwraps the OriginalType as <see cref="Type"/> from the context type.
        /// </summary>
        /// <param name="type">The contextual type</param>
        public static implicit operator Type(CachedType type)
        {
            return type.OriginalType;
        }

        internal CachedType(Type type)
        {
            OriginalType = type;
        }

        /// <summary>
        /// Gets the original type (i.e. without unwrapping Nullable{T}).
        /// </summary>
        public Type OriginalType { get; }

        /// <summary>
        /// Gets all type attributes.
        /// </summary>
        public virtual IEnumerable<Attribute> Attributes => InheritedAttributes;

        /// <summary>
        /// Gets the type name.
        /// </summary>
        public string TypeName => _typeName ??= Type.Name;

        /// <summary>
        /// Gest the original's type info.
        /// </summary>
        public TypeInfo TypeInfo => _typeInfo ??= Type.GetTypeInfo();

        private TypeInfo? _typeInfo;

        /// <summary>
        /// Gets the type's associated attributes of the type (inherited).
        /// </summary>
        public Attribute[] InheritedAttributes
        {
            get
            {
                // TODO: Add type attributes property (only direct attributes without inherit)

                if (_inheritedAttributes != null)
                {
                    return _inheritedAttributes;
                }

                UpdateOriginalGenericArguments();
                lock (this)
                {
                    if (_inheritedAttributes == null)
                    {
                        _inheritedAttributes = _type!
                            .GetTypeInfo()
                            .GetCustomAttributes(true)
                            .OfType<Attribute>()
                            .ToArray();
                    }

                    return _inheritedAttributes;
                }
            }
        }

        /// <summary>
        /// Gets the actual unwrapped type (e.g. gets T of a Nullable{T} type).
        /// </summary>
        public Type Type
        {
            get
            {
                UpdateOriginalGenericArguments();
                return _type ?? throw new InvalidOperationException("_type is not initialized");
            }
        }

        /// <summary>
        /// Gets a value indicating whether this type is wrapped with Nullable{T}.
        /// </summary>
        public bool IsNullableType
        {
            get
            {
                UpdateOriginalGenericArguments();
                return _isNullableType;
            }
        }

        /// <summary>
        /// Gets the type's generic arguments (Nullable{T} is unwrapped).
        /// </summary>
        public CachedType[] GenericArguments
        {
            get
            {
                UpdateOriginalGenericArguments();
                return (CachedType[]?)_genericArguments ?? throw new InvalidOperationException("_genericArguments is not initialized");
            }
        }

        /// <summary>
        /// Gets the type's original generic arguments (Nullable{T} is not unwrapped).
        /// </summary>
        public CachedType[] OriginalGenericArguments
        {
            get
            {
                UpdateOriginalGenericArguments();
                return (CachedType[]?)_originalGenericArguments ?? throw new InvalidOperationException("_genericArguments is not initialized");
            }
        }

        /// <summary>
        /// Gets the type's element type (i.e. array type).
        /// </summary>
        public CachedType? ElementType
        {
            get
            {
                UpdateOriginalGenericArguments();
                return _elementType as CachedType;
            }
        }

        /// <summary>
        /// Gets an attribute of the given type which is defined on the type.
        /// </summary>
        /// <typeparam name="T">The attribute type.</typeparam>
        /// <returns>The attribute or null.</returns>
        public T? GetInheritedAttribute<T>()
            where T : Attribute
        {
            return InheritedAttributes.GetSingleOrDefault<T>();
        }

        /// <summary>
        /// Gets the attributes of the given type which are defined on the type.
        /// </summary>
        /// <typeparam name="T">The attribute type.</typeparam>
        /// <returns>The attributes.</returns>
        public IEnumerable<T> GetInheritedAttributes<T>()
        {
            return InheritedAttributes.OfType<T>();
        }

        /// <inheritdocs />
        public override string ToString()
        {
            var result = Type.Name.FirstToken('`') + "\n  " +
                string.Join("\n", GenericArguments.Select(a => a.ToString())).Replace("\n", "\n  ");

            return result.Trim();
        }

        /// <summary>Gets the cached type for the given type and nullable flags index.</summary>
        /// <param name="type">The type.</param>
        /// <param name="nullableFlagsIndex">The flags.</param>
        /// <returns>The cached type.</returns>
        protected virtual CachedType GetCachedType(Type type, ref int nullableFlagsIndex)
        {
            return type.ToCachedType();
        }

        /// <summary>
        /// Updates the original generic arguments.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UpdateOriginalGenericArguments()
        {
            var nullableFlagsIndex = 0;
            UpdateOriginalGenericArguments(ref nullableFlagsIndex);
        }

        /// <summary>
        /// Updates the original generic arguments.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UpdateOriginalGenericArguments(ref int nullableFlagsIndex)
        {
            if (_originalGenericArguments == null)
            {
                lock (this)
                {
                    if (_originalGenericArguments == null)
                    {
                        var arguments = new List<CachedType>();
                        foreach (var type in OriginalType.GenericTypeArguments)
                        {
                            arguments.Add(GetCachedType(type, ref nullableFlagsIndex));
                        }

                        if (arguments.Count == 0)
                        {
                            var elementType = OriginalType.GetElementType();
                            if (elementType != null)
                            {
                                _elementType = GetCachedType(elementType, ref nullableFlagsIndex);
                            }
                        }

                        _originalGenericArguments = arguments.ToArray();
                        _isNullableType = OriginalType.Name == "Nullable`1";
                        _genericArguments = _isNullableType ? arguments[0]._originalGenericArguments : _originalGenericArguments;
                        _type = _isNullableType ? ((IEnumerable)_originalGenericArguments).Cast<CachedType>().First().OriginalType : OriginalType;
                    }
                }
            }
        }
    }
}
