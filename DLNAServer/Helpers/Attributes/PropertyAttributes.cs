namespace DLNAServer.Helpers.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class LowercaseAttribute : Attribute
    {
        public string PropertyName { get; }
        public LowercaseAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
}
