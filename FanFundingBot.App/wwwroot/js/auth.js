// Вход через новую Telegram Login library: Telegram.Login.auth -> POST /api/auth/telegram {idToken} -> cookie -> redirect.
// Отдельный файл (не инлайн): кэшируется, CSP-friendly.
function telegramLoginFromButton(el) {
    const err = document.getElementById('login-error');
    if (err) err.hidden = true;

    const clientId = el && el.getAttribute('data-client-id');
    if (!clientId || typeof Telegram === 'undefined' || !Telegram.Login) {
        if (err) err.hidden = false;
        return;
    }

    Telegram.Login.auth(
        { client_id: Number(clientId), scope: ['profile', 'write'], lang: 'ru' },
        function (data) { onTelegramLoginData(data); }
    );
}

async function onTelegramLoginData(data) {
    const params = new URLSearchParams(window.location.search);
    const back = params.get('returnUrl');
    const target = back && back.startsWith('/') && !back.startsWith('//') ? back : '/projects';

    const err = document.getElementById('login-error');
    if (!data || data.error || !data.id_token) {
        if (err) err.hidden = false;
        return;
    }

    let res;
    try {
        res = await fetch('/api/auth/telegram', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ idToken: data.id_token })
        });
    } catch {
        res = null;
    }

    if (res && res.ok) {
        window.location.href = target;
        return;
    }

    if (err) err.hidden = false;
}
