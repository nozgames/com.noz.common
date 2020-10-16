#pragma once

#import <UIKit/UIKit.h>

@interface UnityHapticPlugin : NSObject{ }

+ (UnityHapticPlugin*) shared;
- (void) notification:(UINotificationFeedbackType) type;
- (void) selection;
- (void) impact:(UIImpactFeedbackStyle) style;

+ (BOOL) isSupport;

@end
