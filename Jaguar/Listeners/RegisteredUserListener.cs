﻿using Jaguar.Core;

namespace Jaguar.Listeners;

public abstract class RegisteredUserListener<TUser, TRequest> where TUser : User
{
    public string Name { get; set; }
    
    public abstract void Config();
    public abstract Task OnMessageReceived(TUser sender, TRequest request);
}

public abstract class RegisteredUserListener<TUser, TRequest, TResponse> where TUser : User
{
    public string Name { get; set; }
    
    public abstract void Config();
    public abstract Task<TResponse> OnMessageReceived(TUser sender, TRequest request);
}