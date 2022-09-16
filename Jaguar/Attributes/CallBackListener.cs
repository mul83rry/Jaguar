namespace Jaguar.Attributes
{
    [AttributeUsage(AttributeTargets.ReturnValue)]
    public class CallBackListener : Attribute
    {
        public string Name { get; set; }

        public CallBackListener(string name) => Name = name;
    }
}