// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
function openLoginWindow()
{
    document.getElementById('LogInForm').style.display = 'block';
    document.getElementById('containerToBlurr').classList.add('blurrEffect');
}

function closeLoginWindow() {
    document.getElementById('LogInForm').style.display = 'none';
    document.getElementById('containerToBlurr').classList.remove('blurrEffect');
}

function loginFailed(falschmeldung)
{
    if (falschmeldung === 'Failed' && document.getElementById('username').value != null || document.getElementById('username').value != "" &&
        document.getElementById('password').value != null || document.getElementById('password').value != "")
    {
        alert('Anmeldung Falsch');
        document.getElementById('HomeButton').click();
        document.getElementById('username').removeAttribute('value');
        document.getElementById('password').removeAttribute('value');
    }
}

var isLoggedIn;
function checkCookie(isStartseite)
{

    //document.getElementById("categorySelect").select2();

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

function removeCookie()
{
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
    document.getElementById(inputID).value = `${document.cookie}`.split(`${name}=`)[1].split('%2B')[0];
}

function checkMode()
{
    if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches)
    {
        //alert("darkmode");
        //dark mode
        //document.body.style.backgroundColor = "#282b30";
        //document.head.style.backgroundColor = "#282b30";
        //document.body.style.color = "grey";
        //document.getElementById("navbaryeah").style.backgroundColor = "#282b30";

        //////////////////////////////////////////////////////////////////////////////////
        //var css = 'html {-webkit-filter: invert(100%);' +
        //    '-moz-filter: invert(100%);' +
        //    '-o-filter: invert(100%);' +
        //    '-ms-filter: invert(100%); }',

        //    head = document.getElementsByTagName('head')[0],
        //    style = document.createElement('style');

        //// a hack, so you can "invert back" clicking the bookmarklet again
        //if (!window.counter) { window.counter = 1; } else {
        //    window.counter++;
        //    if (window.counter % 2 == 0) { var css = 'html {-webkit-filter: invert(0%); -moz-filter:    invert(0%); -o-filter: invert(0%); -ms-filter: invert(0%); }' }
        //};

        //style.type = 'text/css';
        //if (style.styleSheet) {
        //    style.styleSheet.cssText = css;
        //} else {
        //    style.appendChild(document.createTextNode(css));
        //}

        ////injecting the css to the head
        //head.appendChild(style);
        //////////////////////////////////////////////////////////////////////////////////


        //var all = document.getElementsByTagName("*");
        //for (var i = 0, max = all.length; i < max; i++) {
        //    // Do something with the element here
        //    if(all[i].tagName == "BUTTON")
        //    {
        //        all[i].style.cssText = 'color: green !important';

        //    }
           
        //}
    }
    else
    {


    }
}

