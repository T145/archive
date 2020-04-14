include theos/makefiles/common.mk

BUNDLE_NAME = NCTweeter
NCTweeter_FILES = NCTweeterController.m
NCTweeter_INSTALL_PATH = /Library/WeeLoader/Plugins
NCTweeter_FRAMEWORKS = UIKit CoreGraphics Twitter

include $(THEOS_MAKE_PATH)/bundle.mk

after-install::
	install.exec "killall -9 SpringBoard"
