// Small progressive enhancements. Everything that matters (who is signed in, what they may see)
// is decided on the server; this file only adds convenience on top.
(function () {
    'use strict';

    const loginOverlay = document.getElementById('login-overlay');
    const pageContent = document.getElementById('page-content');

    function setLoginOpen(open) {
        if (!loginOverlay) {
            return;
        }
        loginOverlay.classList.toggle('open', open);
        pageContent.classList.toggle('blurred', open);
        if (open) {
            document.getElementById('username').focus();
        }
    }

    // Login overlay: [data-open-login] opens it, [data-close-login] and Escape close it.
    document.addEventListener('click', function (event) {
        if (event.target.closest('[data-open-login]')) {
            setLoginOpen(true);
        } else if (event.target.closest('[data-close-login]')) {
            setLoginOpen(false);
        }
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            setLoginOpen(false);
        }
    });

    // Destructive forms carry data-confirm="question" and are only submitted after a confirmation.
    document.addEventListener('submit', function (event) {
        const question = event.target.dataset.confirm;
        if (question && !window.confirm(question)) {
            event.preventDefault();
        }
    });
})();
