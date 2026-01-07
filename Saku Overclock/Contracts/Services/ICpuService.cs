using Saku_Overclock.SmuEngine.SmuMailBoxes;
using static Saku_Overclock.Services.CpuService;

namespace Saku_Overclock.Contracts.Services;

public interface ICpuService
{
    /// <summary>
    ///     Флаг доступности ZenStates-Core
    /// </summary>
    bool IsAvailable
    {
        get;
    }

    /// <summary>
    ///     Кодовое имя процессора - Raven
    /// </summary>
    bool IsRaven
    {
        get;
    }

    /// <summary>
    ///     Кодовое имя процессора - DragonRange
    /// </summary>
    bool IsDragonRange
    {
        get;
    }

    /// <summary>
    ///     Количество всех ядер в кристалле, включая неактивные
    /// </summary>
    uint PhysicalCores
    {
        get;
    }

    /// <summary>
    ///     Карта отключенных ядер
    /// </summary>
    uint[] CoreDisableMap
    {
        get;
    }

    /// <summary>
    ///     Количество активных ядер процессора
    /// </summary>
    uint Cores
    {
        get;
    }

    /// <summary>
    ///     Адреса Rsmu Mailbox
    /// </summary>
    SmuAddressSet Rsmu
    {
        get;
    }

    /// <summary>
    ///     Адреса Mp1 Mailbox
    /// </summary>
    SmuAddressSet Mp1
    {
        get;
    }

    /// <summary>
    ///     Адреса Hsmp Mailbox
    /// </summary>
    SmuAddressSet Hsmp
    {
        get;
    }

    /// <summary>
    ///     Семейство процессора
    /// </summary>
    CpuFamily Family
    {
        get;
    }

    /// <summary>
    ///     Имя процессора
    /// </summary>
    string CpuName
    {
        get;
    }

    /// <summary>
    ///     Режим многопоточности
    /// </summary>
    bool Smt
    {
        get;
    }

    /// <summary>
    ///     Информация о материнской плате:
    ///     Имя,
    ///     Вендор,
    ///     BIOS,
    /// </summary>
    CommonMotherBoardInfo MotherBoardInfo
    {
        get;
    }

    /// <summary>
    ///     Доступность инструкции Avx-512 по кодовому имени процессора
    /// </summary>
    bool Avx512AvailableByCodename
    {
        get;
    }

    /// <summary>
    ///     Кодовое имя
    /// </summary>
    string CpuCodeName
    {
        get;
    }

    /// <summary>
    ///     Версия Smu
    /// </summary>
    string SmuVersion
    {
        get;
    }

    /// <summary>
    ///     Команда для выставления андервольтинга по ядрам для Mp1
    /// </summary>
    uint SmuCoperCommandMp1
    {
        get;
        set;
    }

    /// <summary>
    ///     Команда для выставления андервольтинга по ядрам для Rsmu
    /// </summary>
    uint SmuCoperCommandRsmu
    {
        get;
        set;
    }

    /// <summary>
    ///     Таблица сенсоров устройства, от Smu
    /// </summary>
    float[] PowerTable
    {
        get;
    }

    /// <summary>
    ///     Версия таблицы сенсоров устройства
    /// </summary>
    uint PowerTableVersion
    {
        get;
    }

    /// <summary>
    ///     Эффективная частота ОЗУ
    /// </summary>
    float SocMemoryClock
    {
        get;
    }

    /// <summary>
    ///     Эффективная частота Infinity Fabric
    /// </summary>
    float SocFabricClock
    {
        get;
    }

    /// <summary>
    ///     Напряжение контроллера памяти
    /// </summary>
    float SocVoltage
    {
        get;
    }

    /// <summary>
    ///     Отправить Smu команду
    /// </summary>
    /// <param name="mailbox">Сет адресов куда отправить команду</param>
    /// <param name="command">Команда Smu</param>
    /// <param name="arguments">Аргументы для команды</param>
    /// <returns></returns>
    SmuStatus SendSmuCommand(SmuAddressSet mailbox, uint command, ref uint[] arguments);

    /// <summary>
    ///     Получить принадлежность процессора к семейству по кодовому имени
    /// </summary>
    /// <returns>Принадлежность процессора к семейству</returns>
    CodenameGeneration GetCodenameGeneration();

    /// <summary>
    ///     Это устройство - компьютер
    /// </summary>
    /// <returns>true - Да, false - Нет, null - Не определено</returns>
    bool? IsPlatformPc();

    /// <summary>
    ///     Определить: это устройство - компьютер, по кодовому имени
    /// </summary>
    /// <returns>true - Да, false - Нет, null - Не определено</returns>
    bool? IsPlatformPcByCodename();

    /// <summary>
    ///     Прочитать Msr процессора
    /// </summary>
    /// <param name="index">Индекс Msr</param>
    /// <param name="eax">0-31 биты</param>
    /// <param name="edx">32-63 биты</param>
    /// <returns>true - прочитать удалось, false - не удалось прочитать</returns>
    bool ReadMsr(uint index, ref uint eax, ref uint edx);

    /// <summary>
    ///     Записать Msr процессора
    /// </summary>
    /// <param name="msr">Индекс Msr</param>
    /// <param name="eax">0-31 биты</param>
    /// <param name="edx">32-63 биты</param>
    /// <returns>true - записать удалось, false - не удалось записать</returns>
    bool WriteMsr(uint msr, uint eax, uint edx);

    /// <summary>
    ///     Вернуть конфигурацию ОЗУ
    /// </summary>
    /// <returns>Конфигурация ОЗУ</returns>
    MemoryConfig GetMemoryConfig();

    /// <summary>
    ///     Создаёт маску ядра (используется для андервольтинга)
    /// </summary>
    /// <param name="core">Ядро</param>
    /// <param name="ccd">CCD</param>
    /// <param name="ccx">CCX</param>
    /// <returns>Маска ядра</returns>
    uint MakeCoreMask(uint core = 0u, uint ccd = 0u, uint ccx = 0u);

    /// <summary>
    ///     Установить андервольтинг для одного ядра
    /// </summary>
    /// <param name="coreMask">Маска ядра</param>
    /// <param name="margin">Значение андервольтинга</param>
    void SetCoperSingleCore(uint coreMask, int margin);

    /// <summary>
    ///     Обновляет таблицу сенсоров устройства
    /// </summary>
    void RefreshPowerTable();

    /// <summary>
    ///     Возвращает значение множителя процессора по конкретному ядру
    /// </summary>
    /// <param name="core">Ядро</param>
    /// <returns>Значение множителя</returns>
    double GetCoreMultiplier(int core);

    /// <summary>
    ///     Получить температуру процессора (используется как fallback)
    /// </summary>
    /// <returns>Температура процессора</returns>
    float? GetCpuTemperature();
}