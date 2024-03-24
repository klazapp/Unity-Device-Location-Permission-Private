namespace com.Klazapp.Utility
{
    public interface IDeviceLocationPermission
    {
        void DeviceCameraLocationCallback(bool isGranted, float latitude = 0f, float longitude = 0f, float altitude = 0f, float horizontalAccuracy = 0f);
    }
}
