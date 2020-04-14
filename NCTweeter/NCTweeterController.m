#import "BBWeeAppController-Protocol.h"
#import "Twitter/Twitter"

static NSBundle *_NCTweeterWeeAppBundle = nil;

@interface NCTweeterController: NSObject <BBWeeAppController> {
	UIView *_view;
	UIImageView *_backgroundView;
}
@property (nonatomic, retain) UIView *view;
@end

@implementation NCTweeterController
@synthesize view = _view;

+ (void)initialize {
	_NCTweeterWeeAppBundle = [[NSBundle bundleForClass:[self class]] retain];
}

- (id)init {
	return self;
}

- (void)dealloc {
	[_view release];
	[_backgroundView release];
	[super dealloc];
}

- (void)loadFullView {
	// Add subviews to _backgroundView (or _view) here.
	UIButton *b = [UIButton buttonWithType:UIButtonTypeCustom];
	[b setBackgroundImage:[UIImage imageWithContentsOfFile:[_NCTweeterWeeAppBundle pathForResource:@"gfx/tweet" ofType:@"png"]] forState:UIControlStateNormal];
	b.frame = CGRectMake(2, 0, 77, 30);
	[b addTarget:self action:@selector(composeTweet) forControlEvents:UIControlEventTouchDown];
	[_view addSubview:b];
}

- (void)composeTweet {
	TweetComposeViewController *v = [[TweetComposeViewController alloc]init];
	UIViewController *vc = [[UIViewController alloc]init];
	v.completionHandler = ^(TweetComposeViewControllerResult r) {
		if (r == TweetComposeViewControllerResultCancelled) {
			[vc dismissModalViewControllerAnimated:YES];
		}
	};
	vc.view = _view;
	[vc presentModalViewController:v animated:YES];
	[vc release];
	[v release];
}

- (void)loadPlaceholderView {
	// This should only be a placeholder - it should not connect to any servers or perform any intense data loading operations.
	// All widgets are 316 points wide. Image size calculations match those of the Stocks widget.
	_view = [[UIView alloc] initWithFrame:(CGRect){CGPointZero, {316.f, [self viewHeight]}}];
	_view.autoresizingMask = UIViewAutoresizingFlexibleWidth;

	UIImage *bgImg = [UIImage imageWithContentsOfFile:@"/System/Library/WeeAppPlugins/StocksWeeApp.bundle/WeeAppBackground.png"];
	UIImage *stretchableBgImg = [bgImg stretchableImageWithLeftCapWidth:floorf(bgImg.size.width / 2.f) topCapHeight:floorf(bgImg.size.height / 2.f)];
	_backgroundView = [[UIImageView alloc] initWithImage:stretchableBgImg];
	_backgroundView.frame = CGRectInset(_view.bounds, 2.f, 0.f);
	_backgroundView.autoresizingMask = UIViewAutoresizingFlexibleWidth;
	[_view addSubview:_backgroundView];
}

- (void)unloadView {
	[_view release];
	_view = nil;
	[_backgroundView release];
	_backgroundView = nil;
	// Destroy any additional subviews you added here. Don't waste memory :(.
}

- (float)viewHeight {
	return 71.f;
}

@end
