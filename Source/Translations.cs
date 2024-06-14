namespace AICore;

public static class Translations
{
        public static readonly Dictionary<SupportedLanguage, string> Welcome = new()
    {
        { SupportedLanguage.Arabic, @"مرحبًا بك في RimWorldAI!

لقد قمت بتنزيل الوظيفة الإضافية، دعنا نبدأ الذكاء الاصطناعي! سنبدأ بتنزيل الملفات اللازمة للذكاء الاصطناعي في الخلفية. قد يستغرق ذلك بعض الوقت حيث أن الملفات تتراوح بين 2 إلى 8 جيجابايت - شكرًا لصبرك!

بمجرد اكتمال عملية الإقلاع، إذا اخترت تعطيل التحديثات التلقائية، فإن كل شيء يعمل محليًا على جهازك دون الحاجة إلى اتصال بالإنترنت. هذا يبقي تفاعلات الذكاء الاصطناعي خاصة وشخصية، تمامًا كما تريد مستعمرتك!

نوصي بشدة بتمكين التحديثات التلقائية للحفاظ على سلاسة التشغيل ولضمان حصولك على أحدث وأفضل الميزات.

قصص سعيدة وأتمنى لمستعمرتك الازدهار!" },
        { SupportedLanguage.ChineseSimplified, @"欢迎来到RimWorldAI！

您已经下载了mod，现在让我们启动AI！我们将开始在后台下载所需的AI文件。这可能需要一点时间，因为文件大小在2到8GB之间 - 感谢您的耐心等待！

一旦引导过程完成，如果您选择禁用自动更新，一切都将在您的机器上本地运行，无需互联网连接。这将保持您的AI交互私密且个人化，正如您的殖民地所希望的！

我们强烈建议启用自动更新，以确保一切顺利运行，并确保您拥有最新最好的功能。

祝您讲述愉快，愿您的殖民地蓬勃发展！" },
        { SupportedLanguage.ChineseTraditional, @"歡迎來到RimWorldAI！

您已經下載了mod，現在讓我們啟動AI！我們將開始在後台下載所需的AI文件。這可能需要一點時間，因為文件大小在2到8GB之間 - 感謝您的耐心等待！

一旦引導過程完成，如果您選擇禁用自動更新，一切都將在您的機器上本地運行，無需互聯網連接。這將保持您的AI交互私密且個人化，正如您的殖民地所希望的！

我們強烈建議啟用自動更新，以確保一切順利運行，並確保您擁有最新最好的功能。

祝您講述愉快，願您的殖民地蓬勃發展！" },
        { SupportedLanguage.Czech, @"Vítejte v RimWorldAI!

Stáhli jste si mod, teď spustíme AI! Začneme stahovat potřebné soubory AI na pozadí. To může chvíli trvat, protože soubory mají velikost mezi 2 a 8 GB - děkujeme za trpělivost!

Jakmile je zaváděcí proces dokončen, pokud se rozhodnete zakázat automatické aktualizace, vše bude fungovat lokálně na vašem zařízení bez potřeby internetového připojení. Tím zůstanou vaše interakce s AI soukromé a osobní, přesně tak, jak si vaše kolonie přeje!

Důrazně doporučujeme povolit automatické aktualizace, aby vše fungovalo hladce a abyste měli k dispozici nejnovější a nejlepší funkce.

Přejeme vám příjemné příběhy a prosperitu vaší kolonie!" },
        { SupportedLanguage.Danish, @"Velkommen til RimWorldAI!

Du har downloadet mod'en, nu lader vi AI'en starte! Vi begynder at downloade de nødvendige AI-filer i baggrunden. Dette kan tage noget tid, da filerne er mellem 2 og 8 GB - tak for din tålmodighed!

Når opstartsprocessen er fuldført, hvis du vælger at deaktivere automatiske opdateringer, vil alt køre lokalt på din enhed uden behov for internetforbindelse. Dette holder dine AI-interaktioner private og personlige, præcis som din koloni ønsker det!

Vi anbefaler kraftigt at aktivere automatiske opdateringer for at sikre en gnidningsfri drift og for at sikre, at du har de nyeste og bedste funktioner.

God fortælling og må din koloni blomstre!" },
        { SupportedLanguage.Dutch, @"Welkom bij RimWorldAI!

Je hebt de mod gedownload, nu laten we de AI starten! We beginnen met het downloaden van de benodigde AI-bestanden op de achtergrond. Dit kan even duren, aangezien de bestanden tussen de 2 en 8 GB groot zijn - bedankt voor je geduld!

Zodra het opstartproces is voltooid, als je ervoor kiest om automatische updates uit te schakelen, draait alles lokaal op je apparaat zonder internetverbinding. Dit houdt je AI-interacties privé en persoonlijk, precies zoals je kolonie wenst!

We raden ten zeerste aan om automatische updates in te schakelen om een soepele werking te garanderen en ervoor te zorgen dat je over de nieuwste en beste functies beschikt.

Veel plezier met vertellen en moge je kolonie bloeien!" },
        { SupportedLanguage.English, @"Welcome to RimWorldAI!

You've downloaded the mod, now let's get the AI up and running!
We'll kick things off by downloading the necessary AI files in the background. This might take a little while as the files are between 2 to 8 GB - thank you for your patience!

Once the bootstrap process is complete, if you choose to disable automatic updates, everything runs locally on your machine, no internet connection required. This keeps your AI interactions personal and private, just how your colony would want it!

We highly recommend enabling automatic updates to keep everything running smoothly and to ensure you have the latest and greatest features.

Happy storytelling and may your colony flourish!" },
        { SupportedLanguage.Estonian, @"Tere tulemast RimWorldAI-sse!

Olete alla laadinud modi, nüüd laseme AI käivitada! Alustame vajalike AI-failide allalaadimist taustal. See võib võtta natuke aega, kuna failide suurus on 2 kuni 8 GB - aitäh teie kannatlikkuse eest!

Kui alglaadimisprotsess on lõpule viidud, kui otsustate automaatsed värskendused keelata, töötab kõik teie masinas lokaalselt ilma internetiühenduseta. See hoiab teie AI suhtluse privaatsena ja isiklikuna, just nagu teie koloonia soovib!

Soovitame tungivalt lubada automaatvärskendused, et tagada sujuv toimimine ja tagada, et teil on uusimad ja parimad funktsioonid.

Õnnelikku jutustamist ja las teie koloonia õitseb!" },
        { SupportedLanguage.Finnish, @"Tervetuloa RimWorldAI:hin!

Olet ladannut modin, nyt käynnistämme AI:n! Aloitamme tarvittavien AI-tiedostojen lataamisen taustalla. Tämä voi kestää jonkin aikaa, sillä tiedostojen koko vaihtelee 2-8 GB välillä - kiitos kärsivällisyydestäsi!

Kun käynnistysprosessi on valmis, jos päätät poistaa automaattiset päivitykset käytöstä, kaikki toimii paikallisesti koneellasi ilman internetyhteyttä. Tämä pitää AI-interaktiot yksityisinä ja henkilökohtaisina, aivan kuten koloniaasikin haluaa!

Suosittelemme lämpimästi ottamaan automaattiset päivitykset käyttöön, jotta toiminta sujuu sujuvasti ja jotta sinulla on käytössäsi uusimmat ja parhaat ominaisuudet.

Iloista tarinankerrontaa ja toivotamme koloniallesi kukoistusta!" },
        { SupportedLanguage.French, @"Bienvenue dans RimWorldAI !

Vous avez téléchargé le mod, maintenant lançons l'IA ! Nous allons commencer à télécharger les fichiers nécessaires à l'IA en arrière-plan. Cela peut prendre un certain temps car les fichiers varient entre 2 et 8 Go - merci de votre patience !

Une fois le processus de démarrage terminé, si vous choisissez de désactiver les mises à jour automatiques, tout fonctionnera localement sur votre machine sans besoin de connexion Internet. Cela permet de garder vos interactions avec l'IA privées et personnelles, tout comme votre colonie le souhaite !

Nous vous recommandons vivement d'activer les mises à jour automatiques pour garantir un fonctionnement fluide et pour vous assurer de disposer des fonctionnalités les plus récentes et les meilleures.

Bonne narration et que votre colonie prospère !" },
        { SupportedLanguage.German, @"Willkommen bei RimWorldAI!

Sie haben das Mod heruntergeladen, jetzt lassen Sie die KI starten! Wir beginnen mit dem Herunterladen der notwendigen KI-Dateien im Hintergrund. Dies kann einige Zeit dauern, da die Dateien zwischen 2 und 8 GB groß sind - danke für Ihre Geduld!

Sobald der Bootprozess abgeschlossen ist, wird alles lokal auf Ihrem Gerät ausgeführt, wenn Sie sich dafür entscheiden, automatische Updates zu deaktivieren, ohne dass eine Internetverbindung erforderlich ist. Dies hält Ihre KI-Interaktionen privat und persönlich, genau wie Ihre Kolonie es möchte!

Wir empfehlen dringend, automatische Updates zu aktivieren, um einen reibungslosen Betrieb zu gewährleisten und sicherzustellen, dass Sie über die neuesten und besten Funktionen verfügen.

Viel Spaß beim Erzählen und möge Ihre Kolonie gedeihen!" },
        { SupportedLanguage.Hungarian, @"Üdvözöljük a RimWorldAI-ban!

Letöltötte a modot, most indítsuk el az AI-t! Megkezdjük a szükséges AI-fájlok letöltését a háttérben. Ez eltarthat egy ideig, mivel a fájlok mérete 2 és 8 GB között van - köszönjük türelmét!

Miután a rendszerindítási folyamat befejeződött, ha úgy dönt, hogy letiltja az automatikus frissítéseket, minden helyben fog működni a gépén, internetkapcsolat nélkül. Ez az AI-interakciókat priváttá és személyessé teszi, ahogy azt a kolóniája szeretné!

Erősen ajánljuk az automatikus frissítések engedélyezését, hogy biztosítsa a zökkenőmentes működést, és hogy a legújabb és legjobb funkciókkal rendelkezzen.

Boldog történetmesélést kívánunk, és azt kívánjuk, hogy kolóniája virágozzon!" },
        { SupportedLanguage.Italian, @"Benvenuto in RimWorldAI!

Hai scaricato la mod, ora avviamo l'IA! Inizieremo a scaricare i file necessari per l'IA in background. Questo potrebbe richiedere un po' di tempo, poiché i file variano tra i 2 e gli 8 GB - grazie per la tua pazienza!

Una volta completato il processo di avvio, se scegli di disabilitare gli aggiornamenti automatici, tutto funzionerà localmente sul tuo dispositivo senza bisogno di una connessione internet. Questo manterrà le tue interazioni con l'IA private e personali, proprio come desidera la tua colonia!

Consigliamo vivamente di abilitare gli aggiornamenti automatici per garantire un funzionamento fluido e per assicurarti di avere le ultime e migliori funzionalità.

Buon racconto e che la tua colonia prosperi!" },
        { SupportedLanguage.Japanese, @"RimWorldAIへようこそ！

modをダウンロードしましたので、AIを開始しましょう！必要なAIファイルのダウンロードをバックグラウンドで開始します。ファイルのサイズが2〜8GBであるため、少し時間がかかることがあります - ご理解いただきありがとうございます！

起動プロセスが完了すると、自動更新を無効にした場合でも、すべてがローカルで動作し、インターネット接続は不要です。これにより、AIとのやり取りがプライベートで個人的なものになります。これはあなたのコロニーが望む通りです！

自動更新を有効にすることを強くお勧めします。これにより、スムーズな操作が保証され、最新かつ最高の機能を利用できます。

楽しい物語をお楽しみください、そしてあなたのコロニーが繁栄しますように！" },
        { SupportedLanguage.Korean, @"RimWorldAI에 오신 것을 환영합니다!

모드를 다운로드했으니 이제 AI를 시작합시다! 백그라운드에서 필요한 AI 파일 다운로드를 시작합니다. 파일 크기가 2에서 8GB 사이이므로 시간이 걸릴 수 있습니다 - 인내심에 감사드립니다!

부팅 과정이 완료되면 자동 업데이트를 비활성화하기로 선택한 경우에도 모든 것이 인터넷 연결 없이 로컬에서 실행됩니다. 이렇게 하면 AI 상호작용이 비공개되고 개인적인 상태로 유지됩니다. 이는 당신의 식민지가 원하는 바입니다!

자동 업데이트를 활성화하여 원활한 작동을 보장하고 최신 및 최고의 기능을 사용할 수 있도록 강력히 권장합니다.

즐거운 이야기와 당신의 식민지가 번영하기를 바랍니다!" },
        { SupportedLanguage.Norwegian, @"Velkommen til RimWorldAI!

Du har lastet ned mod'en, nå lar vi AI'en starte! Vi vil begynne å laste ned de nødvendige AI-filene i bakgrunnen. Dette kan ta litt tid, da filene varierer fra 2 til 8 GB - takk for tålmodigheten din!

Når oppstartsprosessen er fullført, hvis du velger å deaktivere automatiske oppdateringer, vil alt kjøre lokalt på maskinen din uten behov for internettforbindelse. Dette holder AI-interaksjonene dine private og personlige, akkurat som kolonien din ønsker!

Vi anbefaler sterkt å aktivere automatiske oppdateringer for å sikre jevn drift og for å sikre at du har de nyeste og beste funksjonene.

God historiefortelling og må kolonien din blomstre!" },
        { SupportedLanguage.Polish, @"Witamy w RimWorldAI!

Pobrałeś mod, teraz uruchommy AI! Zaczniemy pobierać niezbędne pliki AI w tle. Może to potrwać trochę czasu, ponieważ pliki mają rozmiar od 2 do 8 GB - dziękujemy za cierpliwość!

Po zakończeniu procesu uruchamiania, jeśli zdecydujesz się wyłączyć automatyczne aktualizacje, wszystko będzie działać lokalnie na twoim urządzeniu bez potrzeby łączenia się z Internetem. To sprawia, że twoje interakcje z AI są prywatne i osobiste, tak jak chce tego twoja kolonia!

Zalecamy włączenie automatycznych aktualizacji, aby zapewnić płynne działanie i mieć najnowsze i najlepsze funkcje.

Szczęśliwego opowiadania historii i niech twoja kolonia rozkwita!" },
        { SupportedLanguage.Portuguese, @"Bem-vindo ao RimWorldAI!

Você baixou o mod, agora vamos iniciar a IA! Começaremos a baixar os arquivos necessários para a IA em segundo plano. Isso pode levar algum tempo, pois os arquivos variam de 2 a 8 GB - obrigado pela sua paciência!

Uma vez que o processo de inicialização esteja concluído, se você optar por desativar as atualizações automáticas, tudo será executado localmente no seu dispositivo, sem a necessidade de uma conexão com a Internet. Isso mantém suas interações com a IA privadas e pessoais, exatamente como sua colônia deseja!

Recomendamos fortemente habilitar as atualizações automáticas para garantir uma operação suave e garantir que você tenha os recursos mais recentes e melhores.

Feliz narrativa e que sua colônia prospere!" },
        { SupportedLanguage.PortugueseBrazilian, @"Bem-vindo ao RimWorldAI!

Você baixou o mod, agora vamos iniciar a IA! Começaremos a baixar os arquivos necessários para a IA em segundo plano. Isso pode levar algum tempo, pois os arquivos variam de 2 a 8 GB - obrigado pela sua paciência!

Uma vez que o processo de inicialização esteja concluído, se você optar por desativar as atualizações automáticas, tudo será executado localmente no seu dispositivo, sem a necessidade de uma conexão com a Internet. Isso mantém suas interações com a IA privadas e pessoais, exatamente como sua colônia deseja!

Recomendamos fortemente habilitar as atualizações automáticas para garantir uma operação suave e garantir que você tenha os recursos mais recentes e melhores.

Feliz narrativa e que sua colônia prospere!" },
        { SupportedLanguage.Romanian, @"Bine ați venit la RimWorldAI!

Ați descărcat mod-ul, acum să pornim AI-ul! Vom începe să descărcăm fișierele necesare AI-ului în fundal. Acest lucru poate dura ceva timp, deoarece fișierele variază între 2 și 8 GB - vă mulțumim pentru răbdare!

Odată ce procesul de bootare este complet, dacă alegeți să dezactivați actualizările automate, totul va funcționa local pe dispozitivul dvs. fără a fi nevoie de o conexiune la internet. Acest lucru păstrează interacțiunile dvs. cu AI private și personale, exact așa cum își dorește colonia dvs.!

Recomandăm insistent activarea actualizărilor automate pentru a asigura o funcționare lină și pentru a vă asigura că aveți cele mai noi și mai bune funcționalități.

Povestire fericită și vă dorim ca colonia dvs. să prospere!" },
        { SupportedLanguage.Russian, @"Добро пожаловать в RimWorldAI!

Вы скачали мод, теперь давайте запустим ИИ! Мы начнем загрузку необходимых файлов ИИ в фоновом режиме. Это может занять некоторое время, так как размер файлов составляет от 2 до 8 ГБ - спасибо за ваше терпение!

После завершения процесса загрузки, если вы решите отключить автоматические обновления, все будет работать локально на вашем устройстве без необходимости подключения к Интернету. Это сохранит ваши взаимодействия с ИИ приватными и личными, так, как это хочет ваша колония!

Мы настоятельно рекомендуем включить автоматические обновления, чтобы обеспечить плавную работу и чтобы у вас были самые последние и лучшие функции.

Счастливых рассказов и пусть ваша колония процветает!" },
        { SupportedLanguage.Slovak, @"Vitajte v RimWorldAI!

Stiahli ste si mod, teraz spustíme AI! Začneme sťahovať potrebné súbory AI na pozadí. To môže chvíľu trvať, pretože súbory majú veľkosť od 2 do 8 GB - ďakujeme za vašu trpezlivosť!

Po dokončení procesu zavádzania, ak sa rozhodnete zakázať automatické aktualizácie, všetko bude fungovať lokálne na vašom zariadení bez potreby pripojenia na internet. To zachová vaše interakcie s AI súkromnými a osobnými, presne tak, ako to vaša kolónia chce!

Dôrazne odporúčame povoliť automatické aktualizácie, aby všetko fungovalo hladko a aby ste mali k dispozícii najnovšie a najlepšie funkcie.

Prajeme vám šťastné rozprávanie a nech vaša kolónia prosperuje!" },
        { SupportedLanguage.Spanish, @"¡Bienvenido a RimWorldAI!

Has descargado el mod, ahora vamos a iniciar la IA! Comenzaremos a descargar los archivos necesarios para la IA en segundo plano. Esto puede tardar un poco ya que los archivos varían entre 2 y 8 GB - ¡gracias por tu paciencia!

Una vez que el proceso de arranque esté completo, si decides desactivar las actualizaciones automáticas, todo funcionará localmente en tu máquina sin necesidad de conexión a Internet. Esto mantiene tus interacciones con la IA privadas y personales, ¡justo como tu colonia quiere!

Recomendamos encarecidamente habilitar las actualizaciones automáticas para garantizar un funcionamiento sin problemas y asegurarte de tener las últimas y mejores características.

¡Feliz narración y que tu colonia prospere!" },
        { SupportedLanguage.SpanishLatin, @"¡Bienvenido a RimWorldAI!

Has descargado el mod, ahora vamos a iniciar la IA! Comenzaremos a descargar los archivos necesarios para la IA en segundo plano. Esto puede tardar un poco ya que los archivos varían entre 2 y 8 GB - ¡gracias por tu paciencia!

Una vez que el proceso de arranque esté completo, si decides desactivar las actualizaciones automáticas, todo funcionará localmente en tu máquina sin necesidad de conexión a Internet. Esto mantiene tus interacciones con la IA privadas y personales, ¡justo como tu colonia quiere!

Recomendamos encarecidamente habilitar las actualizaciones automáticas para garantizar un funcionamiento sin problemas y asegurarte de tener las últimas y mejores características.

¡Feliz narración y que tu colonia prospere!" },
        { SupportedLanguage.Swedish, @"Välkommen till RimWorldAI!

Du har laddat ner modden, nu låter vi AI:n starta! Vi kommer att börja ladda ner de nödvändiga AI-filerna i bakgrunden. Detta kan ta lite tid eftersom filerna varierar mellan 2 och 8 GB - tack för ditt tålamod!

När uppstartsprocessen är klar, om du väljer att inaktivera automatiska uppdateringar, kommer allt att köras lokalt på din enhet utan behov av internetanslutning. Detta håller dina AI-interaktioner privata och personliga, precis som din koloni vill!

Vi rekommenderar starkt att aktivera automatiska uppdateringar för att säkerställa smidig drift och för att se till att du har de senaste och bästa funktionerna.

Glad berättelse och må din koloni blomstra!" },
        { SupportedLanguage.Turkish, @"RimWorldAI'ye hoş geldiniz!

Modu indirdiniz, şimdi AI'yi başlatalım! Gerekli AI dosyalarını arka planda indirmeye başlayacağız. Bu biraz zaman alabilir çünkü dosyalar 2 ile 8 GB arasında değişiyor - sabrınız için teşekkür ederiz!

Başlatma işlemi tamamlandığında, otomatik güncellemeleri devre dışı bırakmayı seçerseniz, her şey internet bağlantısına ihtiyaç duymadan cihazınızda yerel olarak çalışacaktır. Bu, AI etkileşimlerinizi özel ve kişisel tutar, tıpkı koloninizin istediği gibi!

Sorunsuz bir operasyon sağlamak ve en son ve en iyi özelliklere sahip olduğunuzdan emin olmak için otomatik güncellemeleri etkinleştirmenizi şiddetle tavsiye ederiz.

Mutlu hikaye anlatımı ve koloninizin gelişmesi dileğiyle!" },
        { SupportedLanguage.Ukrainian, @"Ласкаво просимо до RimWorldAI!

Ви завантажили мод, тепер давайте запустимо ІІ! Ми почнемо завантажувати необхідні файли ІІ у фоновому режимі. Це може зайняти деякий час, оскільки розмір файлів становить від 2 до 8 ГБ - дякуємо за ваше терпіння!

Після завершення процесу завантаження, якщо ви вирішите відключити автоматичні оновлення, все буде працювати локально на вашому пристрої без потреби в підключенні до Інтернету. Це дозволяє зберігати ваші взаємодії з ІІ приватними і особистими, як того бажає ваша колонія!

Ми настійно рекомендуємо увімкнути автоматичні оновлення, щоб забезпечити плавну роботу і мати найновіші і найкращі функції.

Щасливого розповідання історій і нехай ваша колонія процвітає!" }
    };
}
