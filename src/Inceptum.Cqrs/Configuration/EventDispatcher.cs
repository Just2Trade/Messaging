﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Inceptum.Cqrs.Configuration
{
    internal class EventDispatcher
    {
        readonly Dictionary<Type, List<Action<object>>> m_Handlers = new Dictionary<Type, List<Action<object>>>();
        private readonly string m_BoundedContext;

        public EventDispatcher(string boundedContext)
        {
            m_BoundedContext = boundedContext;
        }
        public void Wire(object o, params OptionalParameter[] parameters)
        {
            parameters = parameters.Concat(new OptionalParameter[] {new OptionalParameter<string>("boundedContext", m_BoundedContext)}).ToArray();

            var handleMethods = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Handle" && 
                    !m.IsGenericMethod && 
                    m.GetParameters().Length>0 && 
                    !m.GetParameters().First().ParameterType.IsInterface)
                .Select(m=>new
                    {
                        method=m,
                        eventType = m.GetParameters().First().ParameterType,
                        callParameters=m.GetParameters().Skip(1).Select(p=>new
                            {
                                parameter = p,
                                optionalParameter=parameters.FirstOrDefault(par=>par.Name==p.Name||par.Name==null && p.ParameterType==par.Type),
                            })
                    })
                .Where(m=>m.callParameters.All(p=>p.parameter!=null));


            foreach (var method in handleMethods)
            {
                registerHandler(method.eventType, o, method.callParameters.ToDictionary(p => p.parameter, p => p.optionalParameter.Value));
            }
        }

        private void registerHandler(Type eventType, object o, Dictionary<ParameterInfo,object> optionalParameters)
        {
            var @event = Expression.Parameter(typeof(object), "event");
         
            Expression[] parameters =
                new Expression[] {Expression.Convert(@event, eventType)}.Concat(optionalParameters.Select(p => Expression.Constant(p.Value))).ToArray();
            var call = Expression.Call(Expression.Constant(o), "Handle", null, parameters);
            var lambda = (Expression<Action<object>>)Expression.Lambda(call, @event);

            List<Action<object>> list;
            if (!m_Handlers.TryGetValue(eventType, out list))
            {
                list = new List<Action<object>>();
                m_Handlers.Add(eventType, list);
            }
            list.Add(lambda.Compile());

        }

        public void Dispacth(object @event)
        {
            List<Action<object>> list;
            if (!m_Handlers.TryGetValue(@event.GetType(), out list))
                return;
            foreach (var handler in list)
            {
                handler(@event);
                //TODO: event handling
            }
        }
    }
}