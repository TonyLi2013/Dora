﻿using Microsoft.Extensions.Options;
using System.Linq.Expressions;
using System.Reflection;

namespace Dora.Interception.Expressions
{
    [NonInterceptable]
    internal class ExpressionInterceptorProvider : InterceptorProviderBase, IInterceptorRegistry
    {
        #region Fields
        private readonly IConventionalInterceptorFactory _conventionalInterceptorFactory;
        private readonly Dictionary<Type, List<Func<Sortable<InvokeDelegate>>>> _interceptorsAccessors4Type = new();
        private readonly Dictionary<int, List<Func<Sortable<InvokeDelegate>>>> _interceptorsAccessors4Method = new();
        private readonly Dictionary<int, List<Sortable<InvokeDelegate>>> _interceptors = new();
        private readonly HashSet<Type> _suppressedTypes = new();
        private readonly HashSet<MethodInfo> _suppressedMethods = new();
        #endregion

        #region Constructors
        public ExpressionInterceptorProvider(IConventionalInterceptorFactory conventionalInterceptorFactory, IOptions<InterceptionOptions> optionsAccessor) : base(conventionalInterceptorFactory)
        {
            _conventionalInterceptorFactory = conventionalInterceptorFactory ?? throw new ArgumentNullException(nameof(conventionalInterceptorFactory));
            var registrations = (optionsAccessor ?? throw new ArgumentNullException(nameof(optionsAccessor))).Value.InterceptorRegistrations;
            registrations?.Invoke(this);
        }


        #endregion

        #region Public methods
        public override bool CanIntercept(Type targetType, MethodInfo method, out bool suppressed)
        {
            Guard.ArgumentNotNull(targetType);
            Guard.ArgumentNotNull(method);
            if (_suppressedTypes.Contains(targetType) || _suppressedMethods.Contains(method))
            {
                suppressed = true;
                return false;
            }
            suppressed = false;
            return _interceptorsAccessors4Type.ContainsKey(targetType) || _interceptorsAccessors4Method.ContainsKey(method.MetadataToken);
        }
        public IInterceptorRegistry<TInterceptor> For<TInterceptor>(params object[] arguments)
            => new InterceptorRegistry<TInterceptor>(_interceptorsAccessors4Type, _interceptorsAccessors4Method, () => _conventionalInterceptorFactory.CreateInterceptor(typeof(TInterceptor), arguments));
        public override IEnumerable<Sortable<InvokeDelegate>> GetInterceptors(Type targetType, MethodInfo method)
        {
            Guard.ArgumentNotNull(targetType);
            Guard.ArgumentNotNull(method);

            var key = method.MetadataToken;
            if (_interceptors.TryGetValue(key, out var interceptors))
            {
                return interceptors;
            }

            if (_interceptorsAccessors4Type.TryGetValue(targetType, out var interceptorAccessors))
            {
                interceptors = interceptorAccessors.Select(it => it()).ToList();
            }

            if (_interceptorsAccessors4Method.TryGetValue(key, out interceptorAccessors))
            {
                if (interceptors is null)
                {
                    interceptors = interceptorAccessors.Select(it => it()).ToList();
                }
                else
                {
                    interceptors.AddRange(interceptorAccessors.Select(it => it()));
                }
            }

            if (interceptors?.Any() ?? false)
            {
                _interceptors[key] = interceptors;
                return interceptors;
            }

            return Enumerable.Empty<Sortable<InvokeDelegate>>();
        }

        public IInterceptorRegistry SupressGetMethod<TTarget>(Expression<Func<TTarget, object?>> propertyAccessor)
        {
            var property = MemberUtilities.GetProperty(propertyAccessor);
            var method = property.GetMethod;
            if (method is not null)
            {
                _suppressedMethods.Add(method);
                return this;
            }
            throw new ArgumentException($"Specified property '{property.Name}' of '{typeof(TTarget)}' does not have Get method.", nameof(propertyAccessor));
        }
        public IInterceptorRegistry SupressMethod<TTarget>(Expression<Action<TTarget>> methodCall)
        {
            if (methodCall == null) throw new ArgumentNullException(nameof(methodCall));
            var method = MemberUtilities.GetMethod(methodCall);
            _suppressedMethods.Add(method);
            return this;
        }
        public IInterceptorRegistry SupressMethods(params MethodInfo[] methods)
        {
            Array.ForEach(methods, it => _suppressedMethods.Add(it));
            return this;
        }
        public IInterceptorRegistry SupressProperty<TTarget>(Expression<Func<TTarget, object?>> propertyAccessor)
        {
            var property = MemberUtilities.GetProperty(propertyAccessor);
            var method = property.GetMethod;
            if (method is not null)
            {
                _suppressedMethods.Add(method);
            }
            method = property.SetMethod;
            if (method is not null)
            {
                _suppressedMethods.Add(method);
            }
            return this;
        }
        public IInterceptorRegistry SupressSetMethod<TTarget>(Expression<Func<TTarget, object?>> propertyAccessor)
        {
            var property = MemberUtilities.GetProperty(propertyAccessor);
            var method = property.SetMethod;
            if (method is not null)
            {
                _suppressedMethods.Add(method);
                return this;
            }
            throw new ArgumentException($"Specified property '{property.Name}' of '{typeof(TTarget)}' does not have Set method.", nameof(propertyAccessor));
        }
        public IInterceptorRegistry SupressType<TTarget>()
        {
            _suppressedTypes.Add(typeof(TTarget));
            return this;
        }
        public IInterceptorRegistry SupressTypes(params Type[] types)
        {
            Array.ForEach(types, it => _suppressedTypes.Add(it));
            return this;
        }
        #endregion

        #region Private methods
        private sealed class InterceptorRegistry<TInterceptor> : IInterceptorRegistry<TInterceptor>
        {
            private readonly Dictionary<Type, List<Func<Sortable<InvokeDelegate>>>> _interceptorAccessors4Type;
            private readonly Dictionary<int, List<Func<Sortable<InvokeDelegate>>>> _interceptorAccessors4Method;
            private readonly Func<InvokeDelegate> _interceptorFatory;
            public InterceptorRegistry(Dictionary<Type, List<Func<Sortable<InvokeDelegate>>>> interceptorAccessors4Type, Dictionary<int, List<Func<Sortable<InvokeDelegate>>>> interceptorAccessors4Method, Func<InvokeDelegate> interceptorFatory)
            {
                _interceptorAccessors4Type = interceptorAccessors4Type;
                _interceptorAccessors4Method = interceptorAccessors4Method;
                var lazy = new Lazy<InvokeDelegate>(() => interceptorFatory());
                _interceptorFatory = () => lazy.Value;
            }

            public IInterceptorRegistry<TInterceptor> ToMethod<TTarget>(int order, Expression<Action<TTarget>> methodCall)
            {
                if (methodCall == null)
                {
                    throw new ArgumentNullException(nameof(methodCall));
                }

                var method = MemberUtilities.GetMethod(methodCall);
                return ToMethods<TTarget>(order, method);
            }

            public IInterceptorRegistry<TInterceptor> ToGetMethod<TTarget>(int order, Expression<Func<TTarget, object?>> propertyAccessor)
            {
                if (propertyAccessor == null)
                {
                    throw new ArgumentNullException(nameof(propertyAccessor));
                }
                var property = MemberUtilities.GetProperty(propertyAccessor);
                var getMethod = property.GetMethod;
                if (getMethod is null)
                {
                    throw new InterceptionException($"Specified property '{property.Name}' of '{property.DeclaringType}' does not have Get method.");
                }
                return ToMethods<TTarget>(order, getMethod);
            }

            public IInterceptorRegistry<TInterceptor> ToSetMethod<TTarget>(int order, Expression<Func<TTarget, object?>> propertyAccessor)
            {
                if (propertyAccessor == null)
                {
                    throw new ArgumentNullException(nameof(propertyAccessor));
                }
                var property = MemberUtilities.GetProperty(propertyAccessor);
                var setMethod = property.SetMethod;
                if (setMethod is null)
                {
                    throw new InterceptionException($"Specified property '{property.Name}' of '{property.DeclaringType}' does not have Set method.");
                }
                return ToMethods<TTarget>(order, setMethod);
            }

            public IInterceptorRegistry<TInterceptor> ToProperty<TTarget>(int order, Expression<Func<TTarget, object?>> propertyAccessor)
            {
                if (propertyAccessor == null)
                {
                    throw new ArgumentNullException(nameof(propertyAccessor));
                }
                var property = MemberUtilities.GetProperty(propertyAccessor);
                var method = property.GetMethod;
                var valid = false;
                if (method is not null && MemberUtilities.IsInterfaceOrVirtualMethod(method))
                {
                    ToMethods<TTarget>(order, method);
                    valid = true;
                }

                method = property.SetMethod;
                if (method is not null && MemberUtilities.IsInterfaceOrVirtualMethod(method))
                {
                    ToMethods<TTarget>(order, method);
                    valid = true;
                }

                if (!valid)
                {
                    throw new InterceptionException($"Interceptor is applied to the property '{property.Name}' of type '{typeof(TTarget)}', whose Get/Set methods are not virtual method or interface implementation method.");
                }

                return this;
            }           

            public IInterceptorRegistry<TInterceptor> ToMethods<TTarget>(int order, params MethodInfo[] methods)
            {
                var targetType = typeof(TTarget);
                if (!targetType.IsPublic && !targetType.IsNestedPublic)
                {
                    throw new InterceptionException($"The interceptor '{typeof(TInterceptor)}' must not be applied to non-public type {targetType}.");
                }

                foreach (var method in methods)
                {
                    if (!MemberUtilities.IsInterfaceOrVirtualMethod(method))
                    {
                        throw new InterceptionException($"The interceptor '{typeof(TInterceptor)}' is applied to the method '{method.Name}' of type '{targetType}', which is neither virtual method nor interface implementation method.");
                    }
                }

                foreach (var method in methods)
                {
                    var key = method.MetadataToken;
                    var list = _interceptorAccessors4Method.TryGetValue(key, out var value)
                        ? value
                        : _interceptorAccessors4Method[key] = new List<Func<Sortable<InvokeDelegate>>>();
                    list.Add(() => new Sortable<InvokeDelegate>(order, _interceptorFatory()));
                }
                return this;
            }

            public IInterceptorRegistry<TInterceptor> ToAllMethods<TTarget>(int order)
            {
                var targetType = typeof(TTarget);
                if (!targetType.IsPublic && !targetType.IsNested)
                {
                    throw new InterceptionException($"The interceptor '{typeof(TInterceptor)}' must not be applied to non-public type {targetType}.");
                }
                var list = _interceptorAccessors4Type.TryGetValue(targetType, out var value)
                   ? value
                   : _interceptorAccessors4Type[targetType] = new List<Func<Sortable<InvokeDelegate>>>();
                list.Add(() => new Sortable<InvokeDelegate>(order, _interceptorFatory()));
                return this;
            }
        }
        #endregion
    }
}
