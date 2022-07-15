namespace Jaguar.Attributes
{
    [AttributeUsage(AttributeTargets.ReturnValue)]
    public class AsyncListener : Attribute
    {
        public string Name { get; set; }

        public AsyncListener(string name) => Name = name;
    }
}