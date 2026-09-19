#if !__has_feature(objc_arc)
#error GameSDKDevice.mm must be compiled with -fobjc-arc.
#endif

#import <Foundation/Foundation.h>
#import <Security/Security.h>
#import <AdSupport/AdSupport.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>
#include <stdlib.h>
#include <string.h>

namespace {
    NSMutableDictionary *CreateQuery(const char *account, const char *service, const char *identifier) {
        if (account == nullptr || service == nullptr || identifier == nullptr) return nil;
        NSString *accountString = [NSString stringWithUTF8String:account];
        NSString *serviceString = [NSString stringWithUTF8String:service];
        NSString *identifierString = [NSString stringWithUTF8String:identifier];
        if (accountString.length == 0 || serviceString.length == 0 || identifierString.length == 0) return nil;
        return [@{
            (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrAccount: accountString,
            (__bridge id)kSecAttrService: serviceString,
            (__bridge id)kSecAttrGeneric: [identifierString dataUsingEncoding:NSUTF8StringEncoding]
        } mutableCopy];
    }

    OSStatus CopyString(NSString *string, char **value) {
        const char *utf8 = string.UTF8String;
        if (utf8 == nullptr) return errSecDecode;
        char *copy = strdup(utf8);
        if (copy == nullptr) return errSecAllocate;
        *value = copy;
        return errSecSuccess;
    }

    OSStatus QueryValue(NSMutableDictionary *identity, char **value) {
        NSMutableDictionary *query = [identity mutableCopy];
        query[(__bridge id)kSecReturnData] = @YES;
        query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
        CFTypeRef result = nullptr;
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
        // Transfer even unexpected result types or failed-call output to ARC for cleanup.
        id object = CFBridgingRelease(result);
        if (status != errSecSuccess) return status;
        if (![object isKindOfClass:[NSData class]]) return errSecDecode;
        NSData *data = (NSData *)object;
        if (data.length == 0 || memchr(data.bytes, 0, data.length) != nullptr) return errSecDecode;
        NSString *string = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        if (string.length == 0) return errSecDecode;
        return CopyString(string, value);
    }
}

extern "C" int GameSDKDevice_GetDeviceId(const char *account, const char *service,
        const char *identifier, const char *description, char **value) {
    if (value == nullptr) return errSecParam;
    *value = nullptr;
    @autoreleasepool {
        NSMutableDictionary *identity = CreateQuery(account, service, identifier);
        NSString *descriptionString = description == nullptr ? nil : [NSString stringWithUTF8String:description];
        if (identity == nil || descriptionString == nil) return errSecParam;

        OSStatus status = QueryValue(identity, value);
        if (status != errSecItemNotFound) return status;

        CFUUIDRef uuid = CFUUIDCreate(kCFAllocatorDefault);
        if (uuid == nullptr) return errSecAllocate;
        NSString *newValue = CFBridgingRelease(CFUUIDCreateString(kCFAllocatorDefault, uuid));
        CFRelease(uuid);
        if (newValue == nil) return errSecAllocate;

        NSMutableDictionary *item = [identity mutableCopy];
        item[(__bridge id)kSecValueData] = [newValue dataUsingEncoding:NSUTF8StringEncoding];
        item[(__bridge id)kSecAttrDescription] = descriptionString;
        NSDate *now = [NSDate date];
        item[(__bridge id)kSecAttrCreationDate] = now;
        item[(__bridge id)kSecAttrModificationDate] = now;
        status = SecItemAdd((__bridge CFDictionaryRef)item, nullptr);
        if (status == errSecDuplicateItem) {
            // The original full query must match; never delete or broaden a conflicting item.
            return QueryValue(identity, value);
        }
        if (status != errSecSuccess) return status;
        return CopyString(newValue, value);
    }
}

extern "C" int GameSDKDevice_DeleteDeviceId(const char *account, const char *service, const char *identifier) {
    @autoreleasepool {
        NSMutableDictionary *query = CreateQuery(account, service, identifier);
        if (query == nil) return errSecParam;
        OSStatus status = SecItemDelete((__bridge CFDictionaryRef)query);
        return status == errSecItemNotFound ? errSecSuccess : status;
    }
}

extern "C" int GameSDKDevice_GetAdvertisingInfo(char **value, int *enabled) {
    if (value != nullptr) *value = nullptr;
    if (enabled != nullptr) *enabled = 0;
    if (value == nullptr || enabled == nullptr) return errSecParam;
    @autoreleasepool {
        BOOL allowed;
        if (@available(iOS 14.0, *)) {
            allowed = ATTrackingManager.trackingAuthorizationStatus == ATTrackingManagerAuthorizationStatusAuthorized;
        } else {
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
            allowed = ASIdentifierManager.sharedManager.isAdvertisingTrackingEnabled;
#pragma clang diagnostic pop
        }

        NSString *idfa = allowed ? ASIdentifierManager.sharedManager.advertisingIdentifier.UUIDString : @"";
        BOOL available = idfa.length > 0 && ![idfa isEqualToString:@"00000000-0000-0000-0000-000000000000"];
        OSStatus status = CopyString(available ? idfa : @"", value);
        if (status == errSecSuccess) *enabled = available ? 1 : 0;
        return status;
    }
}

extern "C" void GameSDKDevice_FreeString(char *value) {
    free(value);
}
