mergeInto(LibraryManager.library, {
    IsMobileUserAgent: function() {
        var userAgent = navigator.userAgent || navigator.vendor || window.opera;

        // 모바일 기기 감지 (Android, iOS, Windows Phone 등)
        var isMobile = /android|webos|iphone|ipad|ipod|blackberry|iemobile|opera mini/i.test(userAgent.toLowerCase());

        // 태블릿도 터치 UI가 필요하므로 모바일로 간주
        var isTablet = /(tablet|ipad|playbook|silk)|(android(?!.*mobile))/i.test(userAgent.toLowerCase());

        return isMobile || isTablet;
    },

    RequestWebGLFullScreen: function() {
        var userAgent = navigator.userAgent || navigator.vendor || window.opera;
        var isIOS = /iPad|iPhone|iPod/.test(userAgent) && !window.MSStream;

        // iOS 전용 브라우저 UI 자동 숨김 함수
        function hideIOSBrowserUI() {
            console.log("🍎 [iOS] Hiding browser UI");

            // 1. 스크롤하여 주소창 숨기기
            setTimeout(function() {
                window.scrollTo(0, 1);
            }, 0);

            // 2. viewport 설정 업데이트
            var viewport = document.querySelector('meta[name="viewport"]');
            if (viewport) {
                viewport.setAttribute('content',
                    'width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover');
            } else {
                viewport = document.createElement('meta');
                viewport.name = 'viewport';
                viewport.content = 'width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover';
                document.head.appendChild(viewport);
            }

            // 3. body 스타일 조정
            document.body.style.margin = "0";
            document.body.style.padding = "0";
            document.body.style.overflow = "hidden";
            document.body.style.position = "fixed";
            document.body.style.width = "100%";
            document.body.style.height = "100%";

            // 4. 캔버스 컨테이너 조정
            var canvas = document.querySelector("#unity-canvas") || document.querySelector("canvas");
            if (canvas) {
                var container = canvas.parentElement;
                if (container) {
                    container.style.width = "100%";
                    container.style.height = "100%";
                    container.style.position = "fixed";
                    container.style.top = "0";
                    container.style.left = "0";
                }

                canvas.style.width = "100%";
                canvas.style.height = "100%";
                canvas.style.display = "block";
            }

            // 5. 터치 시작 시 다시 주소창 숨기기
            document.addEventListener('touchstart', function() {
                window.scrollTo(0, 1);
            }, { once: true });

            console.log("✅ [iOS] Browser UI hidden");
        }

        // iOS인 경우 브라우저 UI 자동 숨김 사용
        if (isIOS) {
            console.log("🍎 iOS detected - using browser UI auto-hide");
            hideIOSBrowserUI();
            return;
        }

        // iOS가 아닌 경우 (Android 등) 전체화면 API 사용
        var canvas = document.querySelector("#unity-canvas") || document.querySelector("canvas");

        if (!canvas) {
            console.warn("Canvas element not found for fullscreen request");
            return;
        }

        // 전체화면 API 호출 (브라우저 호환성 처리)
        var requestFullScreen = canvas.requestFullscreen ||
                                canvas.webkitRequestFullscreen ||
                                canvas.mozRequestFullScreen ||
                                canvas.msRequestFullscreen;

        if (requestFullScreen) {
            requestFullScreen.call(canvas).then(function() {
                console.log("✅ Entered fullscreen mode");
            }).catch(function(err) {
                console.warn("⚠️ Fullscreen request failed:", err);
            });
        } else {
            console.warn("⚠️ Fullscreen API not supported");
        }
    }
});
