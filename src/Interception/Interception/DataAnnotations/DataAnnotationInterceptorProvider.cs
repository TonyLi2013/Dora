﻿using System.Reflection;

namespace Dora.Interception
{
    internal class DataAnnotationInterceptorProvider : IInterceptorProvider
    {
        private readonly IConventionalInterceptorFactory _conventionalInterceptorFactory;
        public DataAnnotationInterceptorProvider(IConventionalInterceptorFactory conventionalInterceptorFactory)
        {
            _conventionalInterceptorFactory = conventionalInterceptorFactory ?? throw new ArgumentNullException(nameof(conventionalInterceptorFactory));
        }

        public IEnumerable<Sortable<InvokeDelegate>> GetInterceptors(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            var type = method.DeclaringType!;

            var list = new List<Sortable<InvokeDelegate>>();
            foreach (var attribute in method.DeclaringType!.GetCustomAttributes<InterceptorAttribute>())
            {
                list.Add(new Sortable<InvokeDelegate>(attribute.Order, _conventionalInterceptorFactory.CreateInterceptor(attribute.Interceptor, attribute.Arguments)));
            }

            foreach (var attribute in method.GetCustomAttributes<InterceptorAttribute>())
            {
                list.Add(new Sortable<InvokeDelegate>(attribute.Order, _conventionalInterceptorFactory.CreateInterceptor(attribute.Interceptor, attribute.Arguments)));
            }

            if (method.IsSpecialName && (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")))
            {
                if (MemberUtilities.TryGetProperty(method, out var property))
                {
                    foreach (var attribute in property!.GetCustomAttributes<InterceptorAttribute>())
                    {
                        list.Add(new Sortable<InvokeDelegate>(attribute.Order, _conventionalInterceptorFactory.CreateInterceptor(attribute.Interceptor, attribute.Arguments)));
                    }
                }
            }
            return list;
        }

        public bool IsInterceptionSuppressed(MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            var type = method.DeclaringType!;
            if (method.IsSpecialName && (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")))
            {
                if(MemberUtilities.TryGetProperty(method,out var property) && property!.GetCustomAttribute<NonInterceptableAttribute>() is not null)
                {
                    return true;
                }
            }
            return type.GetCustomAttribute<NonInterceptableAttribute>() is not null || method.GetCustomAttribute<NonInterceptableAttribute>() is not null;
        }

        private readonly BindingFlags _bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        public void Validate(Type type, Action<MethodInfo> methodValidator, Action<PropertyInfo> propertyValidator)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            foreach (var method in type.GetMethods(_bindingFlags).Where(it=>it.GetCustomAttribute<InterceptorAttribute>() is not null))
            {
                methodValidator(method);
               
            }

            foreach (var property in type.GetProperties(_bindingFlags).Where(it => it.GetCustomAttribute<InterceptorAttribute>() is not null))
            {
                propertyValidator(property);

            }
        }
    }
}