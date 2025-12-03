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

        // iOS 전용 화면 맞춤 함수
        function adjustIOSScreen() {
            console.log("🍎 [iOS] Adjusting screen to fit available viewport");

            // 1. 실제 사용 가능한 화면 크기 가져오기 (주소창 제외)
            var availableWidth = window.innerWidth;
            var availableHeight = window.innerHeight;

            console.log("📐 Available screen:", availableWidth, "x", availableHeight);

            // 2. viewport 설정
            var viewport = document.querySelector('meta[name="viewport"]');
            if (viewport) {
                viewport.setAttribute('content',
                    'width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover, minimal-ui');
            } else {
                viewport = document.createElement('meta');
                viewport.name = 'viewport';
                viewport.content = 'width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover, minimal-ui';
                document.head.appendChild(viewport);
            }

            // 3. body 스타일 - 스크롤 방지 및 고정
            document.body.style.margin = "0";
            document.body.style.padding = "0";
            document.body.style.overflow = "hidden";
            document.body.style.position = "fixed";
            document.body.style.top = "0";
            document.body.style.left = "0";
            document.body.style.width = availableWidth + "px";
            document.body.style.height = availableHeight + "px";

            // 4. 캔버스 컨테이너 조정
            var canvas = document.querySelector("#unity-canvas") || document.querySelector("canvas");
            if (canvas) {
                var container = canvas.parentElement;
                if (container) {
                    container.style.position = "fixed";
                    container.style.top = "0";
                    container.style.left = "0";
                    container.style.width = availableWidth + "px";
                    container.style.height = availableHeight + "px";
                    container.style.overflow = "hidden";
                }

                // 캔버스 크기를 실제 사용 가능한 영역에 맞춤
                canvas.style.width = availableWidth + "px";
                canvas.style.height = availableHeight + "px";
                canvas.style.display = "block";
            }

            // 5. 화면 회전이나 리사이즈 시 재조정
            var resizeTimeout;
            window.addEventListener('resize', function() {
                clearTimeout(resizeTimeout);
                resizeTimeout = setTimeout(function() {
                    var newWidth = window.innerWidth;
                    var newHeight = window.innerHeight;

                    console.log("📐 Screen resized:", newWidth, "x", newHeight);

                    document.body.style.width = newWidth + "px";
                    document.body.style.height = newHeight + "px";

                    if (canvas) {
                        if (container) {
                            container.style.width = newWidth + "px";
                            container.style.height = newHeight + "px";
                        }
                        canvas.style.width = newWidth + "px";
                        canvas.style.height = newHeight + "px";
                    }
                }, 100);
            });

            console.log("✅ [iOS] Screen adjusted to available viewport");
        }

        // iOS인 경우 화면 맞춤 사용
        if (isIOS) {
            console.log("🍎 iOS detected - adjusting to available screen");
            adjustIOSScreen();
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
