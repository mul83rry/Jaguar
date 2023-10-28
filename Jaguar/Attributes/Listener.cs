namespace Jaguar.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class Listener : Attribute
{
    public string Name { get; set; }

    public Listener(string name) => Name = name;
}