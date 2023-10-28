using System.Net;
using Jaguar.Attributes;
using Jaguar.Core;
using static Jaguar.Core.Server;

namespace Jaguar.Manager;

public abstract class ListenersManager
{
    internal static List<ListenersManager> Managers = new();
    private readonly string[] _reservedEventsName = { "JTS", "PRC", "IA", "UDPBYTES" };


    public virtual Task OnBytesReceived(IPEndPoint endPoint, byte[] data)
    {
        return Task.CompletedTask;
    }

    protected ListenersManager()
    {
        AddListeners();
        AddAsyncListeners();

        Managers.Add(this);
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

                var task = new MuTask
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

                if (!AddListener(eventName, task))
                {
                    throw new InvalidOperationException(nameof(eventName));
                }
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
                if (attribute is not CallBackListener listener) continue;

                var eventName = !string.IsNullOrEmpty(listener.Name) ? listener.Name : method.Name;
                var parameters = method.GetParameters();
                    
                var task = new MuTask
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

                if (!AddAsyncListener(eventName, task))
                {
                    throw new InvalidOperationException(nameof(eventName));
                }
            }
        }
    }
}