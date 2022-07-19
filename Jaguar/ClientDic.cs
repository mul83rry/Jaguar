using System.Net;
using Jaguar.Core;
using Jaguar.Core.Processor;

namespace Jaguar;

public class ClientDic
{
    public readonly IPEndPoint Client;
    public User? User;
    internal PostManagement Post;
    internal ReceiptManagement Receipt;
    public DateTime LastActivateTime { get; set; }

    internal ClientDic(User? user, IPEndPoint client)
    {
        User = user;
        Client = client;
        LastActivateTime = DateTime.Now;

        Post = new PostManagement(client);
        Receipt = new ReceiptManagement();
        Receipt.Init();
        Post.Init();
    }
}