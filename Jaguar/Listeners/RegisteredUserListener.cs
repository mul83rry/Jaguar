using Jaguar.Core;

namespace Jaguar.Listeners;

public abstract class RegisteredUserListener<TUser, TData> where TUser : User
{
    public string Name { get; set; }
    
    public abstract void Config();
    public abstract Task OnMessageReceived(TUser sender, TData data);
}