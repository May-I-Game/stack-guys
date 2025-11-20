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
        // Unity 캔버스 요소 찾기
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
