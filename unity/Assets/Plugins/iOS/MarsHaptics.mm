// 짧은 햅틱(C# Haptics.Play → _MarsHaptic). style 0 light · 1 medium · 2 heavy
// 생성기는 세기별로 한 번만 만들어 재사용(ARC·MRC 어느 쪽으로 컴파일돼도 정적 보관으로 해제되지 않음). UIKit 호출은 메인 스레드에서
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *marsGen[3];

extern "C" void _MarsHaptic(int style)
{
    if (style < 0) style = 0;
    if (style > 2) style = 2;
    dispatch_async(dispatch_get_main_queue(), ^{
        if (marsGen[style] == nil)
        {
            UIImpactFeedbackStyle s = style == 0 ? UIImpactFeedbackStyleLight : (style == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy);
            marsGen[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
        }
        [marsGen[style] prepare];
        [marsGen[style] impactOccurred];
    });
}
