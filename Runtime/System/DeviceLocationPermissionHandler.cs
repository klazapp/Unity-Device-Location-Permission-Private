using System;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace com.Klazapp.Utility
{
    public class DeviceLocationPermissionHandler : MonoBehaviour, IDeviceLocationPermission
    {
        #region Variables
        [SerializeField]
        [Tooltip("Toggle this to set script's singleton status. Status will be set on script's OnAwake function")]
        private ScriptBehavior scriptBehavior = ScriptBehavior.None;
        public static DeviceLocationPermissionHandler Instance { get; private set; }
        
        //Callback
        private Action<bool, float, float, float, float> onDeviceLocationPermissionCallback;
        
        //Wfs
        private readonly WaitForSeconds oneWfs = new(1f);
        
        //Location accuracy
        private const float DESIRED_ACCURACY_IN_METERS = 1F;
        private const float UPDATE_DISTANCE_IN_METERS = 0.001F;
        
        //Continuous update
        private bool isContinuousUpdateActive = false;
        private float currentContinuousUpdateDuration = 0f;
        private const float MAX_CONTINUOUS_UPDATE_DURATION = 60F;
        private float continuousUpdateLatitude;
        private float continuousUpdateLongitude;
        private float continuousUpdateAltitude;
        private float continuousUpdateHorizontalAccuracy;
        private event Action<bool> OnContinuousUpdateStatusChanged;
        #endregion
        
        #region Lifecycle Flow
        private void Awake()
        {
            SetScriptBehaviour(scriptBehavior);
        }
        #endregion
        
        #region Public Access
        public static bool GetDeviceLocationPermission()
        {
            return Input.location.isEnabledByUser;
        }
        
        public void RequestDeviceLocationPermission(Action<bool, float, float, float, float> deviceLocationPermissionCallback = null)
        {
            onDeviceLocationPermissionCallback = deviceLocationPermissionCallback;
        
#if UNITY_EDITOR
            DeviceCameraLocationCallback(true, 0f, 0f, 0f, 0f);
#elif UNITY_ANDROID
            StartCoroutine(CheckAndRequestLocationPermission());
#elif UNITY_IOS
            StartCoroutine(RequestLocationPermissionCo());
#endif

#if UNITY_ANDROID
        IEnumerator CheckAndRequestLocationPermission()
        {
            // Check if the fine location permission has been granted
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
            {
                // Request permission
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.FineLocation);
                // Wait a short period for the user to respond
                yield return new WaitForSeconds(1);
            }

            // After waiting, check again. If now granted, proceed to request location updates.
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation))
            {
                StartCoroutine(RequestLocationPermissionCo());
            }
            else
            {
                // Permission was denied or not granted in time
                DeviceCameraLocationCallback(false);
            }
        }
#endif

            //StartCoroutine(RequestLocationPermissionCo());

            IEnumerator RequestLocationPermissionCo()
            {
                //Start service before querying location
                Input.location.Start(DESIRED_ACCURACY_IN_METERS, UPDATE_DISTANCE_IN_METERS);

                //Wait until location initializes
                //Max wait time in seconds
                var maxWait = 5f; 

                while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
                {
                    yield return oneWfs;
                    maxWait--;
                }

                //Service did not initialize in 5 seconds
                if (maxWait < 1)
                {
                    DeviceCameraLocationCallback(false);
                    yield break;
                }

                //Connection has failed.
                if (Input.location.status == LocationServiceStatus.Failed)
                {
                    DeviceCameraLocationCallback(false);
                    yield break;
                }

                //Access granted and location value could be retrieved
                DeviceCameraLocationCallback(true, Input.location.lastData.latitude, Input.location.lastData.longitude, Input.location.lastData.altitude, Input.location.lastData.horizontalAccuracy);
            }
        }
        
        public void StartContinuousLocationUpdate(Action<bool> onContinuousUpdateStatusChanged)
        {
            currentContinuousUpdateDuration = 0f;
            isContinuousUpdateActive = true;
        	if (onContinuousUpdateStatusChanged != null)
            {
                OnContinuousUpdateStatusChanged = onContinuousUpdateStatusChanged;
            }

			StartCoroutine(UpdateContinuousLocationCo(onContinuousUpdateStatusChanged));
        }

        public void StopContinuousLocationUpdate()
        {
            isContinuousUpdateActive = false;
            StopCoroutine(UpdateContinuousLocationCo(null));
        }

        public (float latitude, float longitude, float altitude, float horizontalAccuracy) GetContinuousLocationInfo()
        {
            return (continuousUpdateLatitude, continuousUpdateLongitude, continuousUpdateAltitude,
                continuousUpdateHorizontalAccuracy);
        }
        #endregion
        
        #region Modules
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IEnumerator UpdateContinuousLocationCo(Action<bool> onContinuousUpdateStatusChanged)
        {          
            //Start location service if not running
            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                Input.location.Start(DESIRED_ACCURACY_IN_METERS, UPDATE_DISTANCE_IN_METERS);
            }

            //Wait for location service to initialize with timeout - 5 seconds
            var maxWait = 5;
            while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
            {
                yield return new WaitForSeconds(1);
                maxWait--;
            }

            //Handle failure case if does not start within 5 seconds
            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarning("Location service failed to start");
                isContinuousUpdateActive = false;
                OnContinuousUpdateStatusChanged?.Invoke(false);
                yield break;
            }

            OnContinuousUpdateStatusChanged?.Invoke(true);
            
            while (isContinuousUpdateActive && currentContinuousUpdateDuration < MAX_CONTINUOUS_UPDATE_DURATION)
            {
                currentContinuousUpdateDuration += Time.deltaTime;

                if (Input.location.status == LocationServiceStatus.Running)
                {
                    continuousUpdateLatitude = Input.location.lastData.latitude;
                    continuousUpdateLongitude = Input.location.lastData.longitude;
                    continuousUpdateAltitude = Input.location.lastData.altitude;
                    continuousUpdateHorizontalAccuracy = Input.location.lastData.horizontalAccuracy;
                }
                else
                {
#if UNITY_EDITOR
                    continuousUpdateLatitude = 0;
                    continuousUpdateLongitude = 0;
                    continuousUpdateAltitude = 0;
                    continuousUpdateHorizontalAccuracy = 0;
#else
                    Debug.LogWarning("Location service is not running");
#endif
                }

                yield return null;
            }

            isContinuousUpdateActive = false;
            Input.location.Stop();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetScriptBehaviour(ScriptBehavior behavior)
        {
            if (behavior is not (ScriptBehavior.Singleton or ScriptBehavior.PersistentSingleton)) 
                return;
            
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
                
            Instance = this;

            if (behavior == ScriptBehavior.PersistentSingleton)
            {
                DontDestroyOnLoad(this.gameObject);
            }
        }
        #endregion

        #region IDeviceLocationPermission
        public void DeviceCameraLocationCallback(bool isGranted, float latitude = 0f, float longitude = 0f, float altitude = 0f, float horizontalAccuracy = 0f)
        {
            onDeviceLocationPermissionCallback?.Invoke(isGranted, latitude, longitude, altitude, horizontalAccuracy);
        }
        #endregion
    }
}
