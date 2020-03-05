namespace Toggled.Client
{
    public interface IToggledClient 
    {
        bool GetFeatureValue(string featureName);
    }
}
