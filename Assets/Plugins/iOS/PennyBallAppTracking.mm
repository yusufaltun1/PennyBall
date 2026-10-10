#import <Foundation/Foundation.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

#if defined(__cplusplus)
extern "C" {
#endif

/// UnitySendMessage ile ATT sonucunu MetaAppEvents GO'suna iletir.
/// Status: 0=notDetermined, 1=restricted, 2=denied, 3=authorized
void PB_RequestTrackingAuthorization(const char *gameObjectName)
{
    NSString *goName = gameObjectName != NULL
        ? [NSString stringWithUTF8String:gameObjectName]
        : @"MetaAppEvents";

    if (@available(iOS 14, *))
    {
        [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {
            dispatch_async(dispatch_get_main_queue(), ^{
                UnitySendMessage(
                    [goName UTF8String],
                    "OnAttAuthorizationResult",
                    [[NSString stringWithFormat:@"%d", (int)status] UTF8String]);
            });
        }];
    }
    else
    {
        UnitySendMessage([goName UTF8String], "OnAttAuthorizationResult", "3");
    }
}

int PB_GetTrackingAuthorizationStatus(void)
{
    if (@available(iOS 14, *))
    {
        return (int)ATTrackingManager.trackingAuthorizationStatus;
    }

    return 3;
}

#if defined(__cplusplus)
}
#endif
