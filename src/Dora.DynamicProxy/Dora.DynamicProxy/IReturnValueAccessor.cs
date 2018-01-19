﻿using System.Threading.Tasks;

namespace Dora.DynamicProxy
{
    internal class ReturnValueAccessor<TResult>
    {
        private readonly InvocationContext _context;
        public ReturnValueAccessor(InvocationContext context)
        {
            _context = Guard.ArgumentNotNull(context, nameof(context));
        }
        public TResult GetReturnValue(Task task)
        {
            return ((Task<TResult>)_context.ReturnValue).Result;
        }
    }  
}  