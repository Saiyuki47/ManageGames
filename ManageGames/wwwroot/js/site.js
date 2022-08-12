// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
function openLoginWindow() {
    document.getElementById('LogInForm').style.display = 'block';
    document.getElementById('containerToBlurr').classList.add('blurrEffect');
}
function closeLoginWindow() {
    document.getElementById('LogInForm').style.display = 'none';
    document.getElementById('containerToBlurr').classList.remove('blurrEffect');
}
function loginFailed(falschmeldung) {
    if (falschmeldung === 'Failed') {
        alert('Anmeldung Falsch');
    }


}
var isLoggedIn;
function checkCookie(isStartseite) {
    isLoggedIn = false;
    var cookieExsists;

    cookieExsists = document.cookie.split(';').some(c => {
        return c.trim().startsWith('GameSort+' + '=');
    });
    if (!cookieExsists) {

        if (isStartseite != 'Yes') {

            alert('Piss dich');
            document.getElementById('HomeButton').click();
        }
    }
    else {
        reNewCookie();
        isLoggedIn = true;
        setupPageForPremiumUser();
    }

}

function reNewCookie()
{
    document.cookie = `${document.cookie}`+'; Max-Age=600;'
}

function setupPageForPremiumUser() {
    //alert(`${document.cookie}`.split(`${name}=`)[1].split('%2B')[0]);
    var premiumBtns = document.getElementsByClassName('premiumButton');
    //document.getElementsByClassName('premiumButton')[0].classList.remove("premiumButton");
    document.getElementById('logInButton').innerHTML = "Abmelden";
    document.getElementById('logInButton').removeAttribute('onclick');
    document.getElementById('logInButton').setAttribute('onclick', 'removeCookie()');

    for (i = 0; i < premiumBtns.length; i++)
    {
        premiumBtns[i].removeAttribute("style");
    }
}

function removeCookie() {
    document.cookie = 'GameSort+=; Max-Age=0; path=/; domain=' + location.host;
    document.cookie = 'GameSort+=; Max-Age=0; path=/; domain=' + location.hostname;
    //alert('cookie deleted');

    document.getElementById('logInButton').innerHTML = 'Anmelden';
    document.getElementById('logInButton').removeAttribute('onclick');
    document.getElementById('logInButton').setAttribute('onclick', 'openLoginWindow()');
    document.getElementById('HomeButton').click();

    //var premiumBtns = document.getElementsByClassName('BurgerLinks');

    //for (i = 0; i < premiumBtns.length; i++) {
    //    premiumBtns[i].style.display = 'none';
    //}

}
function fillUserID(inputID)
{
    document.getElementById(inputID).value =`${document.cookie}`.split(`${name}=`)[1].split('%2B')[0];
}