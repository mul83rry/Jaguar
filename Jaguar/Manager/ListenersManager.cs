using Jaguar.Attributes;
using Jaguar.Core;
using static Jaguar.Core.Server;

namespace Jaguar.Manager
{
    public abstract class ListenersManager
    {
        private readonly string[] _reservedEventsName = new[] { "JTS", "PRC", "IA", "UDPBYTES" };

        protected ListenersManager()
        {
            AddListeners();
            AddAsyncListeners();
        }

        private void AddListeners()
        {
            var type = GetType();

            foreach (var method in type.GetMethods())
            {
                foreach (Attribute attribute in method.GetCustomAttributes(true))
                {
                    if (attribute is not Listener listener) continue;

                    var eventName = !string.IsNullOrEmpty(listener.Name) ? listener.Name : method.Name;
                    var parameters = method.GetParameters();


                    /*if (parameters[0].ParameterType != typeof(IPEndPoint) && parameters[0].ParameterType != typeof(User))
                        throw new ArgumentException("must have a Sender parameter");*/
                    if (method.ReturnType != typeof(Task))
                        throw new Exception($"Type of method '{method.Name}' must be Task.");

                    var task = new MuTask()
                    {
                        FunctionType = parameters[1].ParameterType,
                        Method = method,
                        ListenersManager = this,
                        SenderType = parameters[0].ParameterType
                    };

                    if (type.Name != "InternalListener")
                    {
                        if (_reservedEventsName.Contains(eventName))
                        {
                            throw new Exception($"This events name are reserved, please use another '{eventName}'");
                        }
                    }

                    AddListener(eventName, task);
                }
            }
        }
        
        private void AddAsyncListeners()
        {
            var type = GetType();

            foreach (var method in type.GetMethods())
            {
                foreach (Attribute attribute in method.ReturnTypeCustomAttributes.GetCustomAttributes(true))
                {
                    if (attribute is not AsyncListener listener) continue;

                    var eventName = !string.IsNullOrEmpty(listener.Name) ? listener.Name : method.Name;
                    var parameters = method.GetParameters();

                    /*if (parameters[0].ParameterType != typeof(IPEndPoint))
                        throw new ArgumentException("must have a Sender parameter");*/
                    /*if (method.ReturnType != typeof(void))
                        throw new Exception("Type of method must be Task.");*/

                    var task = new MuTask()
                    {
                        FunctionType = parameters[1].ParameterType,
                        Method = method,
                        ListenersManager = this,
                        SenderType = parameters[0].ParameterType
                    };

                    if (type.Name != "InternalListener")
                    {
                        if (_reservedEventsName.Contains(eventName))
                        {
                            throw new Exception($"This events name are reserved, please use another '{eventName}'");
                        }
                    }

                    AddAsyncListener(eventName, task);
                }
            }
        }
    }
}