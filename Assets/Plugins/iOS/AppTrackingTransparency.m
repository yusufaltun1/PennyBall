#import <AppTrackingTransparency/AppTrackingTransparency.h>
#import <AdSupport/AdSupport.h>
#import <dispatch/dispatch.h>

typedef void (*PBATTCallback)(int status);

static void PB_InvokeCallback(PBATTCallback callback, int status)
{
    if (callback != NULL)
    {
        callback(status);
    }
}

__attribute__((visibility("default")))
int PB_GetAppTrackingAuthorizationStatus(void)
{
    if (@available(iOS 14, *))
    {
        return (int)[ATTrackingManager trackingAuthorizationStatus];
    }

    return 3;
}

__attribute__((visibility("default")))
void PB_RequestAppTrackingAuthorization(PBATTCallback callback)
{
    if (@available(iOS 14, *))
    {
        ATTrackingManagerAuthorizationStatus currentStatus =
            [ATTrackingManager trackingAuthorizationStatus];

        if (currentStatus != ATTrackingManagerAuthorizationStatusNotDetermined)
        {
            PB_InvokeCallback(callback, (int)currentStatus);
            return;
        }

        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:
            ^(ATTrackingManagerAuthorizationStatus status) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    PB_InvokeCallback(callback, (int)status);
                });
            }];
        return;
    }

    PB_InvokeCallback(callback, 3);
}
