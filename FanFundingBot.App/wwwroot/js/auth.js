// Вход через Telegram Login Widget: POST /api/auth/telegram -> cookie -> redirect.
// Отдельный файл (не инлайн): кэшируется, CSP-friendly.
async function onTelegramAuth(user) {
    const params = new URLSearchParams(window.location.search);
    const back = params.get('returnUrl');
    const target = back && back.startsWith('/') && !back.startsWith('//') ? back : '/projects';

    let res;
    try {
        res = await fetch('/api/auth/telegram', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                id: user.id,
                firstName: user.first_name,
                lastName: user.last_name,
                username: user.username,
                photoUrl: user.photo_url,
                authDate: user.auth_date,
                hash: user.hash
            })
        });
    } catch {
        res = null;
    }

    if (res && res.ok) {
        window.location.href = target;
        return;
    }

    const err = document.getElementById('login-error');
    if (err) err.hidden = false;
}
