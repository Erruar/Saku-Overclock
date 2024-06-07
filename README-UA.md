<!-- Readme Start -->
<a name="readme-top"></a>
<!--
*** Thanks for checking out the Best-README-Template. If you have a suggestion
*** that would make this better, please fork the repo and create a pull request
*** or simply open an issue with the tag "enhancement".
*** Don't forget to give the project a star!
*** Thanks again! Now go create something AMAZING! :D
-->



<!-- PROJECT SHIELDS -->
<!--
*** I'm using markdown "reference style" links for readability.
*** Reference links are enclosed in brackets [ ] instead of parentheses ( ).
*** See the bottom of this document for the declaration of the reference variables
*** for contributors-url, forks-url, etc. This is an optional, concise syntax you may use.
*** https://www.markdownguide.org/basic-syntax/#reference-style-links
-->
[![Discord][discord-shield]][discord-url]
[![Donalerts][donalerts-shield]][donalerts-url]
[![GPL License][license-shield]][license-url]



<!-- PROJECT LOGO -->
<br />
<div align="center">
  <a href="https://github.com/Erruar/Saku-Overclock">
    <img src="Saku Overclock/Assets/WindowIcon.ico" alt="Logo" width="80" height="80">
  </a>

  <h3 align="center">Saku Overclock</h3>

  <p align="center">
    Краще додаток для розгону ноутбуків, для тих, хто бажає максимум!
    <br />
    <a href="https://github.com/Erruar/Saku-Overclock/releases"><strong>Перейти до релізів »</strong></a>
    <br />
    <br />
    <a href="https://www.youtube.com/watch?v=RToJMa5GZ7Q">Як встановити?</a>
    ·
    <a href="https://github.com/Erruar/Saku-Overclock/issues">У мене баг!</a>
    ·
    <a href="https://github.com/Erruar/Saku-Overclock/issues">Хочу запропонувати</a>
  </p>
</div>



<!-- TABLE OF CONTENTS -->
<details>
  <summary>Короткий список контенту</summary>
  <ol>
    <li>
      <a href="#о-проекте">О проекте</a>
      <ul>
        <li><a href="#галерея-картинок">Галерея картинок</a></li>
        <li><a href="#засноване-на">Засноване на</a></li>
      </ul>
    </li>
    <li>
      <a href="#почнемо">Почнемо</a>
      <ul>
        <li><a href="#вимога">Вимога</a></li>
        <li><a href="#установка">Установка</a></li>
      </ul>
    </li>
    <li><a href="#використання">Використання</a></li>
    <li><a href="#плани-на-майбутнє">Плани на майбутнє</a></li>
    <li><a href="#допомога-в-розробці">Допомога в розробці</a></li>
    <li><a href="#ліцензія">Ліцензія</a></li>
    <li><a href="#звязатися-з-нами">Зв'язатися з нами</a></li>
    <li><a href="#у-проекті-використовувалися">У проекті використовувалися:</a></li>
  </ol>
</details>



<!-- ABOUT THE PROJECT -->
## О проекте

[![Product Name Screen Shot][product-screenshot]](https://github.com/Erruar/Saku-Overclock/blob/master/Images/main.png)

Saku Overclock це неймовірна програма для розгону ноутбуків з процесорами Ryzen, яка дозволяє вам налаштувати різні параметри вашого ноутбука для отримання максимальної продуктивності вашого пристрою. Наша програма дозволить вам:

* **Точна настройка струмів:** управляти струмами VRM, SoC, процесора, і інтегрованою графікою, для отримання точного контролю витрати струмів, збільшення лімітів і отримання кращої стабільності.
* **Управління температурами:** ви можете встановити відразу максимальна межа температури для всіх комплектуючих вашого ноутбука крім дискретної графіки. Це допоможе вам запобігти перегріванню та зробить ваш ноутбук більш ефективним. Коли температура процесора або його підсистем близько або дорівнює виставленої вами максимальній температурі, процесор і/або його підсистеми тут-же почнуть троттл і скидати частоту, щоб підтримувати виставлену вами температуру.
* **Конфігурації частоти і напруг:** встановіть частоти для Power States вашого процесора і їм відповідні напруги для максимальної ефективності, потрібної саме вам. Врахуйте, що частота процесора, також як і напруга, залежить від вкрай багатьох факторів, тому це вкрай малоймовірно, що ви зможете точно ними управляти без істотних модифікацій.
* **Управління кулерами:** змінюйте швидкості ваших кулерів, налаштовуйте їх криву і зберігайте свої конфігурації для швидкого доступу. Створіть свої пресети для максимальної зручності. Врахуйте, що управління кулерами здійснюється за допомогою Notebook fan Controller, його доведеться встановити окремо, а також вам потрібен робочий для вас файл конфігурації під Ваш ноутбук. Моя програма може створювати файли конфігурації, проте вам все одно доведеться його писати самостійно, через конструктивних особливостей ноутбуків кожного виробника, універсального творця конфігурацій під кожну модель немає і не буде.
* **Зручні налаштування, імпорт і експорт:** всі ваші налаштування автоматично зберігаються, а також ви можете їх імпортувати або експортувати для інших користувачів.
* **Моніторинг в реальному часі:** Будьте в курсі всіх показників вашого процесора, температура, відеокарти, ОЗУ, батареї і багато іншого, в реальному часі, завдяки Saku PowerMon Pro.
* **Високий рівень оптимізації:** завдяки ретельній оптимізації, додаток працює плавно і швидко, не витрачаючи на це багато ресурсів. Ви можете його згорнути в системний трей і не турбуватися, що воно буде навантажувати ваш ноутбук.
* **Фонові завдання і автостарт:** програма продовжує застосовувати останні застосовані вами настройки, якщо ви включили це в параметрах програми. Ви також можете включити автостарт разом з системою, для максимальної зручності, щоб ваш розгін був завжди з вами!

З Saku Overclock ви отримуєте повний контроль над вашим ноутбуком, оптимізувавши його продуктивність і виставивши оптимальні для вас температури зробити ваш ноутбук ще краще. 
Звичайно ви можете копіювати елементи моєї програми в своїх проектах, дотримуючись ліцензію та авторські права. У майбутньому я буду розвивати цей проект і додавати багато нових функцій. Дякуємо за вибір Saku Overclock!

Читайте `README-RU.md` Далі, щоб дізнатися, як встановити та використовувати мою програму.

<p align="right">(<a href="#readme-top">back to top</a>)</p>



### Галерея картинок
<details>
  <summary>Main</summary>
  <ol>
    <h1 align="center">Dark main page</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/main.png"></img>
    <p> </p>
    <p align="left">The main page of my application will greet you at launch</p>
    <p> </p>
    <h1 align="center">White and blue main page</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/main-white.png"></img> 
    <p> </p>
    <p align="left">My application has many themes that you can customize for yourself or create your own unique themes that you like! You can adjust the transparency level of both the background image and the darkening mask after it</p>
  </ol>
</details>


<details>
  <summary>Premaded presets</summary>
  <ol>
    <h1 align="center">Minimum premaded presets</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/preset/min.png"></img>
    <p> </p>
    <p align="left">Minimum preset will keep your processor cold, but I don`t recommend to use it under CPU load! Use it only if u have latest battery percent and it is important to be online right now</p>
    <h1 align="center">Eco preset</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/preset/eco.png"></img>
    <p> </p>
    <p align="left">Eco preset will save your battery and keep performance</p>
    <h1 align="center">Balance preset</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/preset/balance.png"></img>
    <p> </p>
    <p align="left">Balance preset will allow you to play more without charging</p>
    <h1 align="center">Speed preset</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/preset/speed.png"></img>
    <p> </p>
    <p align="left">Speed preset will provide better performance than normal</p>
    <h1 align="center">Maximum preset</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/preset/max.png"></img>
    <p> </p>
    <p align="left">Maximum preset will give you almost maximum performance of your cpu</p>
    <p> </p>
  </ol>
</details>


<details>
  <summary>Parameters & Custom presets</summary>
  <ol>
    <h1 align="center">Parameters page</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/main.png"></img>
    <p align="left"> Here you can see the contents of the Parameters page. This page is the most important in the entire application, as it allows you to configure overclocking the way you need it.</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/1.png"></img>
    <p align="left">• CPU Overclocking: Allows users to adjust the power and temperature of their CPU, which can increase performance.</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/2.png"></img>
    <p align="left">• VRM Tuning: Allows users to adjust VRM settings, Currents and timings of your CPU.</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/3.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/4.png"></img>
    <p align="left">• iGPU and CPU subsystem clocks: Allows users to overclock clocks iGPU and other CPU subsystems. Note that this section works and visible only on processors Ryzen Raven Ridge (2000), Ryzen Dali (3000), Zen Athlon (3000) and Ryzen Picasso (3000) line, and changing these parameters DOES NOT GIVE a 100% chance that the frequency will always be equal to what you set. This will affect their base frequency, from which the main frequency is generated. In simple words, these frequencies are influenced by a lot of factors (temperature, load, power), which is why it is very difficult to make sure that the frequency is always the same. Also, I do not give any guarantees that these parameters will be unlocked for you, in case of an error you will see in the notification which parameters were not applied.</p>
    <p> </p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/5.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/6.png"></img>
    <p> </p>
    <p align="left">• Advanced CPU parameters: Allows users to adjust more advanced CPU parameters, which are intended for experienced overclockers and can be risky if not configured correctly. It will works only on Ryzen Renoir (4000) and above processors!</p>
    <p> </p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/7.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/8.png"></img>
    <p> </p>
    <p align="left">• CPU Power States: Allows users to adjust the power states of the CPU, even with system starts (if my app in autostart). everything is the same here as with the frequency of the iGPU and CPU subsystems. There is far from a 100% chance that the frequency will always be the same as you set here or the voltage, however, this way you bring your processor closer to the specified values</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/9.png"></img>
    <p align="left">When you open SMU section - enable Quick commands to apply thems! Autodetect is there, pls DON'T CHANGE RSP, ARG and CMD Adresses! If you know SMU commands for your CPU, you can type them as a hint into Quick note box. You can highlight them if it needed or paste photo there. Autosaving is there</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/10.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/11.png"></img>
    <p align="left">• SMU Parameters editor: Allows users to edit  SMU parameters for extreme overclocking, and is likely best left to experienced overclockers only. These commands can do ANYTHING with your CPU even BURN IT!!! BE CAREFUL! If you know your CPU commands, you can add thems in Quick SMU Commands and apply them by pressing their buttons, by pressing Apply button or with app start (even with autostart with system!). You can give any name, description and icon for your quick command. It's very practical! For example, you need to quickly change the voltage to the desired value or return the frequency to normal or raise the PBO - you just click on the apply button on the desired command and your processor immediately applies this effect! Again, the commands are NOT freely available and you will have to search for them yourself. Please note that the commands are in HEX format!!! To quickly convert from decimal to HEX, highlight your value or just right-click on the Arguments field and select Convert to HEX. Note that you do NOT need to write 0x in the command. The commands usually look like 0x2E, 0x11, this is just an EXAMPLE. You don't need to write 0x. if the command accepts multiple arguments, separate them with "," (like: 17, 19, 20, 80). It can give you best experience of your laptop if you KNOW WHAT ARE YOU DOING. This is not a joke and your processor may well burn out from ignorance of commands</p>
    <p> </p>
  </ol>
</details>



<details>
  <summary>Information page</summary>
  <ol>
    <h1 align="center">Information page</h1>
   <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/info/main.png"></img>
    <p align="left"> Here you can see the contents of the Information page. This page is allowing you to see important values of you system</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/info/1.png"></img>
    <p align="left">• Processor: Allows users to show the current properties of your processor</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/info/2.png"></img>
    <p align="left">• Power Information: Allows users to watch VRM powers, Currents and timings of your CPU</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/info/3.png"></img>
    <p align="left">• RAM Information: Allows users to know their common RAM info. Soon there were be more info options!</p>
    <p> </p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/info/4.png"></img>
    <p align="left">• CPU Power States: There you can see PStates of you CPU</p>
    <p> </p> 
  </ol>
</details>




<details>
  <summary>Cooler page</summary>
  <ol>
    <h1 align="center">Cooler tweaks page</h1> 
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/1.png"></img>
    <p align="left">• Page: There you can adjust your cooler spinning, but only if you have Notebook fan controller app on your pc <strong>AND ONLY IF YOU HAVE WORKING CONFIGURATION FOR YOUR LAPTOP!</strong> Just download it and install in C:/ drive. Without it app will crash at this pages. Then in my app set your laptop model at which Notebook fan controller is working for you and you can see and change values! How to use it - simply click on suggest button (button with question symbol). Choose config from saved configs. Choose Enabled in Fan Control status, choose target speed or auto. Autosaving is there. If you wanna more - go to Advanced mode, where you can change whole all config and fan curve!</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/2.png"></img>
    <p align="left">• Suggest button: It can help you to find configs which will (NOT 100%!!!) work with your laptop</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/3.png"></img>
    <p align="left">• Advanced Cooler Tweaking, Readme: There you can found an example of config, copy it to clipboard</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/4.png"></img>
    <p align="left">• Advanced Cooler Tweaking, Fan Curve Editor: Allows users to change fan curves on your laptop. All values have autosaving when you change them! After changing I just recommend you to enter in normal mode and switch to Disabled and Enabled then it should working normally. Or reboot</p>
    <p> </p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/5.png"></img>
    <p align="left">• Advanced Cooler Tweaking, Fan Curve Editor, Color changing: you can highlight your fan curve into other color (haven't autosaving)</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/6.png"></img>
    <p> </p>
    <p align="left">• Advanced Cooler Tweaking, menu: There you can open closed tab (CTRL + F4 to close tab) or edit existed config or create new one: empty (for PRO users), from example or from any others. Note: you can delete custom configs via Saku Overclock only if they have "Custom" in their name</p>
    <p> </p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/7.png"></img>
    <p align="left">• Advanced Cooler Tweaking, Config Editor: You can edit config as you need or remove it and delete for custom. Note: THERE IS NO AUTOSAVING! PRESS ON SAVE BUTTON MANUALLY! NOW THERE IS NO ```CTRL + S```!!!</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/8.png"></img>
    <p> </p>
    <p align="left">• Config Editor, Common configs: You can only rename it, when you press button with icon like pencil</p>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/9.png"></img>
    <p align="left">• Config Editor, Custom configs: You can only rename it or delete PERMANENTLY when you press button with icon like pencil</p>
  </ol>
</details>




<details>
  <summary>Settings</summary>
  <ol>
    <h1 align="center">Settings page</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/settings.png"></img>
    <p> </p>
    <p align="left">There you can add app in autostart with windows, set close app to tray when it opening, set autoapply when opening, check for updates and I recommend to enable - Reapply latest setting every. Enable it and set to 1-7. This value is enough</p>
  </ol>
</details>

### Засноване на

My program was built using Win UI 3 .NET interface with UWP framework and is based on C#. The app uses RyzenAdj for viewing information on information page which is written on C++ programming language, Zen States Core and Collapse launcher elements.
* [![Dotnet][Dotnet.com]][Dotnet-url]
* [![Json][Json.org]][Json-url]
* [![Csharp][Csharp.org]][Csharp-url]
* [![Cplusplus][Cplusplus.com]][Cplusplus-url]

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- GETTING STARTED -->
## Почнемо

Let's figure out how to install my app!
To get a copy of my app and running follow these simple steps:

### Вимога

In order for my app to work properly, you will first need to download Notebook fan controller
[Link to download](https://github.com/hirschmann/nbfc/releases)
If you don't want to control the coolers through my app, you can skip this step.

### Установка

Let's install my app!

1. Get a `Saku Overclock.exe` from releases: [Releases link](https://github.com/Erruar/Saku-Overclock/releases)
2. Double click on downloaded `.exe`
3. Install it 
4. Create desktop link: Go to `C:\Program Files (x86)\Saku Overclock` find a `Saku Overclock.exe` and right click on it. Select **Share** then select **Desktop (Create link)**
5. Open new link from your Desktop
6. Now you have installed my app!

### If you have any troubles 
<a href="https://github.com/Erruar/Saku-Overclock/issues/new"><strong>Seems like app isn't working for me »</strong></a>

## Video installation (Click to open)
[![Playback](https://i.ytimg.com/vi/RToJMa5GZ7Q/maxresdefault.jpg)](https://www.youtube.com/watch?v=RToJMa5GZ7Q)

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- USAGE EXAMPLES -->
## Використання

Using my program is pretty simple! Go to the presets tab and apply the desired one. 
Or if you have a special case (for example, 0.4 GHz issue) or you want to get the most out of your laptop, then the Parameters tab is for you. 
On it, you can set the values that you want, and if you don't know what to do, there are tips and recommendations for balance and performance

If you have found the perfect settings for yourself, just click on the apply button at the bottom right, it looks like a Play Music icon. But before that, I RECOMMEND going to the settings page and making sure that you have enabled "Reapply latest settings every (S)", after which, applying your settings, they will be updated every time, depending on what time you set, because some laptop manufacturers add a Power Limits self-healing protocol to the BIOS so that the laptop does not burn down in case of something. 
### About burning
My program CANNOT cause you to burn down the processor if you do not: 
- Set the maximum temperature above 90 degrees,
- Set extremely high Power Limits and at the same time very low Time of fast and Time of slow frequency rise(S),
- Use SMU parameters without proper knowledge, manuals, warnings or by accident
- If you are trying to create an NBFC configuration file for yourself to control the speed of the cooler, it MAY well STOP Spinning! Take this into account! Create such configurations ONLY when you have set the Fan Service Control Status to Disabled or Read Only,
- If you are trying to increase the frequency above the maximum, I DO NOT GUARANTEE the safety of such actions. All potentially dangerous parameters are marked with a special icon, hover over it and **READ** what it changes before changing it

<a href="https://github.com/Erruar/Saku-Overclock/issues/2"><strong>Seems like app isn't working for me »</strong></a>
<!--_For more examples, please refer to the [Documentation](https://example.com)_-->

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- ROADMAP -->
## Плани на майбутнє

- [x] Add Readme
- [x] Add back to top links
- [x] Add and create first Beta version
- [x] Add and create first Release Candidate version
- [ ] Add and create first Release version
- [ ] Multi-language Support
    - [x] English
    - [x] Russian
    - [ ] More?

See the [open issues](https://github.com/Erruar/Saku-Overclock/issues) for a full list of proposed features (and known issues).

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- CONTRIBUTING -->
## Допомога в розробці

Contributions are what make the open source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion that would make this better, please fork the repo and create a pull request. You can also simply open an issue with the tag "enhancement".
Don't forget to give the project a star! Thanks again!

1. [Fork the Project](https://github.com/Erruar/Saku-Overclock/fork)
2. Create your Feature Branch 
3. Commit your Changes in your fork
4. Push to the Branch 
5. [Open a Pull Request](https://github.com/Erruar/Saku-Overclock/pulls)
6. Wait for acceptiong or rejecting!

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- LICENSE -->
## Ліцензія

Distributed under the GPL-3.0 License. See `LICENSE.md` for more information. The application contains elements of [Collapse Launcher](https://github.com/CollapseLauncher/Collapse), which is licensed by [MIT license](https://github.com/CollapseLauncher/Collapse/blob/main/LICENSE), such elements are marked in the code as [Collapse Launcher](https://github.com/CollapseLauncher/Collapse), also, get acquainted with the [MIT license](https://github.com/CollapseLauncher/Collapse/blob/main/LICENSE) if you want to use their elements too. The GPL and MIT licenses have similar concepts, as a result of which I did not copy their license into my project

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- CONTACT -->
## Зв'язатися з нами

**Our Discord** - [Saku Overclock Community](https://discord.com/invite/eFcP6TSjEZ) - **erruarbrorder@gmail.com**

**Project Link:** [https://github.com/Erruar/Saku-Overclock/](https://github.com/Erruar/Saku-Overclock/)

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- ACKNOWLEDGMENTS -->
## У проекті використовувалися

Here you can see links to everything that was used in the development of the project:
* [Zen States Core](https://github.com/irusanov/ZenStates-Core)
* [Collapse launcher UI elements](https://github.com/CollapseLauncher/Collapse)
* [Notebook fan control](https://github.com/hirschmann/nbfc)
* [Ryzen ADJ](https://github.com/FlyGoat/RyzenAdj)
* [Freepik icons](https://www.freepik.com/)
* [Win UI](https://github.com/microsoft/WinUI-Gallery)

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->
[discord-shield]: https://img.shields.io/badge/Join%20our-discord-%23ff7f50?style=for-the-badge&logo=discord&logoColor=%23ff7f50
[discord-url]: https://discord.gg/WzgsFvgTuh
[donalerts-shield]: https://img.shields.io/badge/Support%20me-DonAlerts-%23f13a13?style=for-the-badge&logo=disqus&logoColor=%23f13a13
[donalerts-url]: https://www.donationalerts.com/r/RubyTrack
[license-shield]: https://img.shields.io/badge/LICENSE%20-GPL-%230ff99C?style=for-the-badge
[license-url]: https://github.com/Erruar/Saku-Overclock/blob/master/LICENSE.md
[product-screenshot]: https://github.com/Erruar/Saku-Overclock/blob/master/Images/main.png
[Dotnet.com]: https://img.shields.io/badge/%20-Dotnet-%23512BD4?style=for-the-badge&logo=dotnet&logoColor=%23FFFFFF&link=https%3A%2F%2Fdotnet.microsoft.com%2F
[Dotnet-url]: https://dotnet.microsoft.com/
[Json.org]: https://img.shields.io/badge/%20-JSON-%23000000?style=for-the-badge&logo=json
[Json-url]: https://www.json.org/json-en.html
[Csharp.org]: https://img.shields.io/badge/%20-C%23%20app-%23512BD4?style=for-the-badge&logo=csharp
[Csharp-url]: https://learn.microsoft.com/en-us/dotnet/csharp/
[Cplusplus.com]: https://img.shields.io/badge/%20-C%2B%2B%20app-%2300599C?style=for-the-badge&logo=cplusplus&logoColor=%23ffffff
[Cplusplus-url]: https://learn.microsoft.com/en-us/cpp/?view=msvc-170
