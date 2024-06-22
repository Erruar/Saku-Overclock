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
    An awesome laptop overclock utility for those who want real performance!
    <br />
    <a href="https://github.com/Erruar/Saku-Overclock/releases"><strong>Explore the releases »</strong></a>
    <br />
    <br />
    <a href="https://www.youtube.com/watch?v=RToJMa5GZ7Q">View installation</a>
    ·
    <a href="https://github.com/Erruar/Saku-Overclock/issues">Report Bug</a>
    ·
    <a href="https://github.com/Erruar/Saku-Overclock/issues">Request Feature</a>
  </p>
</div>
 
<!-- TABLE OF CONTENTS -->
<details>
  <summary>Table of Contents</summary>
  <ol>
    <li>
      <a href="#about-the-project">About The Project</a>
      <ul>
        <li><a href="#pictures">Pictures</a></li>
        <li><a href="#built-with">Built With</a></li>
      </ul>
    </li>
    <li>
      <a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#requirements">Requirements</a></li>
        <li><a href="#installation">Installation</a></li>
      </ul>
    </li>
    <li><a href="#usage">Usage</a></li>
    <li><a href="#roadmap">Roadmap</a></li>
    <li><a href="#contributing">Contributing</a></li>
    <li><a href="#license">License</a></li>
    <li><a href="#contact">Contact</a></li>
    <li><a href="#projects-used">Projects used:</a></li>
  </ol>
</details>



<!-- ABOUT THE PROJECT -->
## About The Project

[![Product Name Screen Shot][product-screenshot]](https://github.com/Erruar/Saku-Overclock/blob/master/Images/main/main.png)

Saku Overclock is an incredible utility for Ryzen laptop overclocking, providing precise control over various parameters to enhance your device's performance. Our program offers:

* Accurate Current Tuning: Manage VRM, SoC, processor, and integrated graphics currents, giving you complete control.
* Temperature Management: Set the maximum temperature for all laptop components, excluding the discrete GPU. This helps prevent overheating and makes your laptop more efficient. When temperatures exceed the set threshold, processor frequencies can automatically decrease to maintain optimal conditions.
* Frequency and Voltage Configuration: Set frequencies for processor performance states and their corresponding voltages to achieve optimal efficiency.
* Fan Control: Regulate fan speed, adjust its curve, and save configurations for quick access. Create custom presets for convenience.
* Settings Persistence and Sharing: All your settings are automatically saved, and you can export/import them, facilitating sharing with other users.
* Real-time Monitoring: Keep track of processor metrics, temperature, GPU, RAM, battery, and more in real-time.
* Resource-Optimized Operation: Thanks to optimization, the application runs smoothly and quickly without burdening unnecessary processor resources.
* Background Operation and Auto-Start: The program can operate in the background or automatically start with the system, ensuring user convenience.

With Saku Overclock, you gain full control over your laptop, optimizing its performance and maintaining optimal temperature conditions.
Of course, you can copy my program in your projects! So I'll be adding more in the near future. Thanks for using it!

Use the `README.md` to get started.

<p align="right">(<a href="#readme-top">back to top</a>)</p>



### Pictures
<details>
  <summary>Main</summary>
  <ol>
    <h1 align="center">Dark theme main page</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/main/main.png"></img>
    <p> </p>
    <p align="left">The main page of my application will greet you at launch</p>
    <p> </p>
    <h1 align="center">White theme main page</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/main/main-white.png"></img> 
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
    <br />
    <br />
    <h1 align="center">Eco preset</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/preset/eco.png"></img>
    <p> </p>
    <p align="left">Eco preset will save your battery and keep performance</p>
    <br />
    <br />
    <h1 align="center">Balance preset</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/preset/balance.png"></img>
    <p> </p>
    <p align="left">Balance preset will allow you to play more without charging</p>
    <br />
    <br />
    <h1 align="center">Speed preset</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/preset/speed.png"></img>
    <p> </p>
    <p align="left">Speed preset will provide better performance than normal</p>
    <br />
    <br />
    <h1 align="center">Maximum preset</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/preset/max.png"></img>
    <p> </p>
    <p align="left">Maximum preset will give you almost maximum performance of your cpu</p>
    <p> </p>
  </ol>
</details>


<details>
  <summary>Overclocking parameters</summary>
  <ol>
    <h1 align="center">Parameters page</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/main.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/main2.png"></img>
    <p align="left"> Here you can see the contents of the Parameters page. This page is the most important in the entire application, as it allows you to configure overclocking the way you need it.</p>
    <br />
    <br />
    <h1 align="center">CPU Overclocking</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_cpu_1.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_cpu_2.png"></img>
    <p align="left">• CPU Overclocking: Allows users to adjust the power and temperature of their CPU, which can significantly increase performance.</p>
    <br />
    <br />
    <h1 align="center">VRM Tuning</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_vrm_1.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_vrm_2.png"></img>
    <p align="left">• VRM Tuning: Allows users to adjust VRM settings, Currents and timings of your CPU. Note: Super dangerous settings are CPU voltage relative and avaivable ONLY on Raven (2000), Dali (3000) and Picasso (3000) CPUs otherwise you shouldn't see those options. I actually don't recommend you to try those options.</p>
    <br />
    <br />
    <h1 align="center">iGPU and CPU subsystem clocks</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_sub_1.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_sub_2.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_sub_3.png"></img>
    <p align="left">• iGPU and CPU subsystem clocks: Allows users to overclock clocks iGPU and other CPU subsystems. Note that this section works and visible only on processors Ryzen Raven Ridge (2000), Ryzen Dali (3000), Zen Athlon (3000) and Ryzen Picasso (3000) line, and changing these parameters DOES NOT GIVE a 100% chance that the frequency will always be equal to what you set. This will affect their base frequency, from which the main frequency is generated. In simple words, these frequencies are influenced by a lot of factors (temperature, load, power), which is why it is very difficult to make sure that the frequency is always the same. Also, I do not give any guarantees that these parameters will be unlocked for you, in case of an error you will see in the notification which parameters were not applied. About "Fix Ryzen 0,4 GHz frequency or fix Ryzen 0,39 GHz frequency: this really can fix Ryzen 0,4 GHz issue but it actually works also only for Ryzen Raven Ridge (2000), Ryzen Dali (3000), Zen Athlon (3000) and Ryzen Picasso (3000) CPU lines. "Max performance" will give your CPU the maximum RAM State - 2400 MHz for CPU and 1000 MHz for iGPU. We CAN NOT adjust it. And one note: fan speed can be MAXIMUM and there is NO WAY to fix it only if you have working for your laptop Notebook Fan Controller configuration. On other Ryzen CPU lines AMD have removed AcBtc state and there is no way to fix this issues on non 2000-3000 CPUs. But I've trying to found a solution! Thank you, testers!</p>
    <br />
    <br />
    <h1 align="center">Advanced CPU parameters</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_adv_1.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_adv_2.png"></img> 
    <p align="left">• Advanced CPU parameters: Allows users to adjust more advanced CPU parameters, which are intended for experienced overclockers and can be risky if not configured correctly. Note: Some BIOS can Reject those options! Don't worry if nothing happens.</p>
    <br />
    <br />
    <h1 align="center">CPU Power States</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_pst_1.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_pst_2.png"></img> 
    <p align="left">• CPU Power States: Allows users to adjust the power states of the CPU, even with system starts (if my app in autostart). everything is the same here as with the frequency of the iGPU and CPU subsystems - "and changing these parameters DOES NOT GIVE a 100% chance that the frequency will always be equal to what you set" because the frequency and voltages of your processor depends on so many factors, which is why it is extremely problematic to set a specific frequency and voltages and make your processor keep it. However, this way you bring your processor closer to the specified values. Note: on some laptops NEEDED to activate OC1 mode in OC MODE in BIOS "AMD CBS/Zen common options" and disable CPB.</p>
    <br />
    <br />
    <h1 align="center">All cores curve optimizer settings</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_co.png"></img>  
    <p align="left">• All cores curve optimizer settings: Allows users to adjust voltage/frequency curve of the CPU or iGPU with custom coefficient from user, even with system starts (if my app in autostart). Those settings gave you maximum control of your CPU voltage.</p>
    <br />
    <br />
   <h1 align="center">Per core curve optimizer settings</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_co_ccd_1.png"></img>  
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_co_ccd_2.png"></img>  
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_co_ccd_3.png"></img>  
    <p align="left">• Per core curve optimizer settings: Allows users to adjust voltage/frequency curve of each CPU core with custom coefficient from user, even with system starts (if my app in autostart). Those settings gave you maximum control of your CPU voltage. This setting is above than "All cores curve optimizer" and "iGPU curve optimizer" settings. You have 3 different modes: Disabled - this section and all those settings are disabled (same if checkbox near this mode selector is unchecked), Saku Laptops - method for almost all Ryzen Laptops, Saku Desktop - method for almost all Ryzen Desktop CPUs *not all Desktop CPUs my app supports, Irusanov method - method for almost all CPUs most universal if others are not work properly. After changing those settings please CHECK stability and voltages! There are safe limits but also check!</p>
    <br />
    <br />
 <h1 align="center">Per core curve optimizer settings+</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_co_ccd_1.png"></img>  
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_co_ccd_2.png"></img>  
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_co_ccd_3.png"></img>  
    <p align="left">• Per core curve optimizer settings+: same with usual "Per core curve optimizer settings" section but for 8+ cores CPUs</p>
    <br />
    <br />
    <h1 align="center">SMU Parameters editor</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_smu_1.png"></img>
    <p align="left">When you open SMU section - enable "Apply SMU commands" to apply them! Autodetect is there, pls DON'T CHANGE RSP, ARG and CMD Adresses! If you know SMU commands for your CPU, you can type them as a hint into Quick note box. You can highlight them if it needed or paste photo there. Autosaving is there. Note: due to the various limitations of various laptop manufacturers, basically even if you know the SMU commands for your processor, there are no guarantees that all commands will work, and there is also no documentation on their use, so I recommend using them in extreme overclocking or only at the request of someone who really understands this topic! These parameters may well irretrievably burn your processor without leaving it a chance. Please note that if the command is blocked, it cannot be unblocked. If the command is "Not found", it is still possible to unlock it, but no one knows how, sometimes completely different actions help. Please note that the first status after application is the status of the Saku Overclock parameters, and the second message with the status of the SMU parameters!</p>
    <br />
    <h3 align="center">Quick SMU commands</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_smu_2.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_smu_4.png"></img>
    <p align="left">• SMU Parameters editor and Quick SMU commands: Allows users to edit  SMU parameters for extreme overclocking, and is likely best left to experienced overclockers only. These commands can do ANYTHING with your CPU even BURN IT!!! BE CAREFUL! If you know your CPU commands, you can add thems in Quick SMU Commands and apply them by pressing their buttons, by pressing Apply button or with app start (even with autostart with system!). You can give any name, description and icon for your quick command. It's very practical! For example, you need to quickly change the voltage to the desired value or return the frequency to normal or raise the PBO - you just click on the apply button on the desired command and your processor immediately applies this effect! Again, the commands are NOT freely available and you will have to search for them yourself. Please note that the commands are in HEX format!!! To quickly convert from decimal to HEX, highlight your value or just right-click on the Arguments field and select Convert to HEX. Note that you do NOT need to write 0x in the command. The commands usually look like 0x2E, 0x11, this is just an EXAMPLE. You don't need to write 0x. if the command accepts multiple arguments, separate them with "," (like: 17, 19, 20, 80). It can give you best experience of your laptop if you KNOW WHAT ARE YOU DOING. This is not a joke and your processor may well burn out from ignorance of commands. You can customize your Quick SMU commands, apply them with app start or with apply button and reapply every seconds automatically</p>
    <br />
    <h3 align="center">Apply range to SMU command</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_smu_3.png"></img> 
    <p align="left">• Apply range to SMU command: You can apply range to one SMU command. Its useful for unlocking all SMU features at once or in some other variants.</p>
    <br />
    <br />
    <h1 align="center">SMU functions manager</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_fun_1.png"></img>  
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_fun_2.png"></img>  
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/custom/params_fun_3.png"></img>  
    <p align="left">• SMU functions manager: Allows users to change SMU functions on their devices. Those settings gave you maximum control of your SMU. There you can change common SMU functions.</p>
    <br />
    <br />
    <p> </p>
  </ol>
</details>



<details>
  <summary>Information page</summary>
  <ol>
    <h1 align="center">Information page</h1>
   <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/info/main.png"></img>
    <p align="left"> Here you can see the contents of the Information page. This page is allowing you to see important values of you system</p>
    <br />
    <br />
    <h1 align="center">Processor</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/info/1.png"></img>
    <p align="left">• Processor: Allows users to show the current properties of your processor</p>
    <br />
    <br />
    <h1 align="center">Power Information</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/info/2.png"></img>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/info/3.png"></img> 
    <p align="left">• Power Information: Allows users to watch VRM powers, Currents and timings of your CPU</p>
    <br />
    <br />
    <h1 align="center">RAM Information</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/info/4.png"></img>
    <p align="left">• RAM Information: Allows users to know their common RAM info. Soon there were be more info options!</p>
    <br />
    <br />
    <h1 align="center">CPU Power States</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/info/5.png"></img>
    <p align="left">• CPU Power States: There you can see PStates of you CPU</p> 
  </ol>
</details>


<details>
  <summary>Saku PowerMon Pro</summary>
  <ol>
    <h1 align="center">PowerMon main window</h1>
   <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/powermon/main.png"></img>
    <p align="left"> Here you can see the entire ALL important values of you system. You can see and add notes to values (for example "GPU clock (MHz)")</p> 
    <br />
    <br />
    <h1 align="center">How to open Saku PowerMon</h1>
   <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/powermon/open_1.png"></img>
   <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/powermon/open_2.png"></img>
    <p align="left"> Here you can see how to open Saku PowerMon Pro</p> 
  </ol>
</details>


<details>
  <summary>Cooler page</summary>
  <ol>
    <h1 align="center">Cooler tweaks page</h1> 
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/1.png"></img>
    <p align="left">• Page: There you can adjust your cooler spinning, but only if you have Notebook fan controller app on your pc <strong>AND ONLY IF YOU HAVE WORKING CONFIGURATION FOR YOUR LAPTOP!</strong> Just download it and install in C:/ drive. Without it app will crash at this pages. Then in my app set your laptop model at which Notebook fan controller is working for you and you can see and change values! How to use it - simply click on suggest button (button with question symbol). Choose config from saved configs. Choose Enabled in Fan Control status, choose target speed or auto. Autosaving is there. If you wanna more - go to Advanced mode, where you can change whole all config and fan curve!</p>
    <br />
    <br />
    <h1 align="center">Suggest button</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/2.png"></img>
    <p align="left">• Suggest button: It can help you to find configs which will (NOT 100%!!!) work with your laptop</p>
    <br />
    <br />
    <h1 align="center">Advanced Cooler Tweaking</h1>
    <h2 align="center">Readme</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/3.png"></img>
    <p align="left">• Advanced Cooler Tweaking, Readme: There you can found an example of config, copy it to clipboard</p>
    <br />
    <br />
    <h2 align="center">Fan Curve Editor</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/4.png"></img>
    <p align="left">• Advanced Cooler Tweaking, Fan Curve Editor: Allows users to change fan curves on your laptop. All values have autosaving when you change them! After changing I just recommend you to enter in normal mode and switch to Disabled and Enabled then it should working normally. Or reboot</p> 
    <br />
    <br />
    <h2 align="center">Fan Curve Editor</h1>
    <h3 align="center">Color changing</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/5.png"></img>
    <p align="left">• Advanced Cooler Tweaking, Fan Curve Editor, Color changing: you can highlight your fan curve into other color (haven't autosaving)</p>
    <br />
    <br />
    <h2 align="center">Main menu</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/6.png"></img> 
    <p align="left">• Advanced Cooler Tweaking, menu: There you can open closed tab (CTRL + F4 to close tab) or edit existed config or create new one: empty (for PRO users), from example or from any others. Note: you can delete custom configs via Saku Overclock only if they have "Custom" in their name</p>
    <br />
    <br />
    <h2 align="center">Config Editor</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/7.png"></img>
    <p align="left">• Advanced Cooler Tweaking, Config Editor: You can edit config as you need or remove it and delete for custom. Note: THERE IS NO AUTOSAVING! PRESS ON SAVE BUTTON MANUALLY! NOW THERE IS NO ```CTRL + S```!!!</p>
    <br />
    <br /> 
    <h1 align="center">Config Editor</h1>
    <h2 align="center">Common configs</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/8.png"></img> 
    <p align="left">• Config Editor, Common configs: You can only rename it, when you press button with icon like pencil</p>
    <br />
    <br />
    <h2 align="center">Custom configs</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/cooler/9.png"></img>
    <p align="left">• Config Editor, Custom configs: You can only rename it or delete PERMANENTLY when you press button with icon like pencil</p>
  </ol>
</details>




<details>
  <summary>Settings</summary>
  <ol>
    <h1 align="center">Settings page</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/settings/settings.png"></img> 
    <p align="left">There you can add app in autostart with windows, set close app to tray when it opening, set autoapply when opening, check for updates and I recommend to enable - Reapply latest setting every. Enable it and set to 1-7. This value is enough</p>
    <br />
    <br />
    <h1 align="center">Settings startup options</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/settings/settings_startup.png"></img> 
    <p align="left">You can chose preferred startup options</p> 
    <p align="left">• No startup options: The application does not run with the system and does not hide from the user in the system tray when launched.</p>
    <p align="left">• Hide to tray: When the application is launched from the user, it will immediately hide in the tray, without distracting you from your work.</p>
    <p align="left">• Startup with OS: The application will run with the system, but will not hide in the tray.</p>
    <p align="left">• Startup & Tray: The application will launch with the system and immediately hide in the tray, without distracting you from your work</p>
    <br />
    <br />
    <h1 align="center">Application themes</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/settings/settings_themes.png"></img> 
    <p align="left">My application has many themes that you can customize for yourself or create your own unique themes that you like! You can adjust the transparency level of both the background image and the darkening mask after it.</p> 
    <h3 align="left">PLEASE, AFTER APPLYING ANY THEME, CHANGE THE WINDOW SIZE SLIGHTLY, JUST ONCE IS ENOUGH AFTER APPLYING THEME, THIS IS IMPORTANT, BECAUSE THE THEME MAY NOT BE APPLIED ADEQUATELY!</h3>
    <br />
    <br />
    <h1 align="center">Advanced theme settings</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/settings/settings_advancedthemes.png"></img> 
    <p align="left">There you can adjust the transparency level of both the background image and the darkening mask after it and change theme background (only on custom themes).</p> 
    <br />
    <br />
    <h1 align="center">Background theme settings</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/settings/settings_themebg.png"></img> 
    <p align="left">There you can change theme background from file or from link to image. Note: on some Windows 10 build we have huge problems with this feature!</p> 
    <br />
    <br />
    <h1 align="center">Theme manager</h1>
    <img src="https://github.com/Erruar/Saku-Overclock/blob/master/Images/settings/settings_thememanager.png"></img> 
    <p align="left">There you can manage all your custom themes or create a new one (New name...). You can change theme names or delete themes.</p> 
  </ol>
</details>

### Built With

My program was built using Win UI 3 .NET interface with UWP framework and is based on C#. The app uses RyzenAdj for viewing information on information page which is written on C++ programming language, Zen States Core and Collapse launcher elements.
* [![Dotnet][Dotnet.com]][Dotnet-url]
* [![Json][Json.org]][Json-url]
* [![Csharp][Csharp.org]][Csharp-url]
* [![Cplusplus][Cplusplus.com]][Cplusplus-url]

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- GETTING STARTED -->
## Getting Started

Let's figure out how to install my app!
To get a copy of my app and running follow these simple steps:

### Requirements

In order for my app to work properly, you will first need to download Notebook fan controller
[Link to download](https://github.com/hirschmann/nbfc/releases)
If you don't want to control the coolers through my app, you can skip this step.

### Installation

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
## Usage

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
## Roadmap

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
## Contributing

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
## License

Distributed under the GPL-3.0 License. See `LICENSE.md` for more information. The application contains elements of [Collapse Launcher](https://github.com/CollapseLauncher/Collapse), which is licensed by [MIT license](https://github.com/CollapseLauncher/Collapse/blob/main/LICENSE), such elements are marked in the code as [Collapse Launcher](https://github.com/CollapseLauncher/Collapse), also, get acquainted with the [MIT license](https://github.com/CollapseLauncher/Collapse/blob/main/LICENSE) if you want to use their elements too. The GPL and MIT licenses have similar concepts, as a result of which I did not copy their license into my project

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- CONTACT -->
## Contact

**Our Discord** - [Saku Overclock Community](https://discord.com/invite/eFcP6TSjEZ) - **erruarbrorder@gmail.com**

**Project Link:** [https://github.com/Erruar/Saku-Overclock/](https://github.com/Erruar/Saku-Overclock/)

<p align="right">(<a href="#readme-top">back to top</a>)</p>



<!-- ACKNOWLEDGMENTS -->
## Projects used:

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
[product-screenshot]: https://github.com/Erruar/Saku-Overclock/blob/master/Images/main/main.png
[Dotnet.com]: https://img.shields.io/badge/%20-Dotnet-%23512BD4?style=for-the-badge&logo=dotnet&logoColor=%23FFFFFF&link=https%3A%2F%2Fdotnet.microsoft.com%2F
[Dotnet-url]: https://dotnet.microsoft.com/
[Json.org]: https://img.shields.io/badge/%20-JSON-%23000000?style=for-the-badge&logo=json
[Json-url]: https://www.json.org/json-en.html
[Csharp.org]: https://img.shields.io/badge/%20-C%23%20app-%23512BD4?style=for-the-badge&logo=csharp
[Csharp-url]: https://learn.microsoft.com/en-us/dotnet/csharp/
[Cplusplus.com]: https://img.shields.io/badge/%20-C%2B%2B%20app-%2300599C?style=for-the-badge&logo=cplusplus&logoColor=%23ffffff
[Cplusplus-url]: https://learn.microsoft.com/en-us/cpp/?view=msvc-170
