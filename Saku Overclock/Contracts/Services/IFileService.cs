namespace Saku_Overclock.Contracts.Services;

public interface IFileService
{
    /// <summary>
    ///     Client read class from file
    /// </summary>
    /// <returns>Class object</returns>
    T? Read<T>(string folderPath, string fileName);

    /// <summary>
    ///     Save class to file
    /// </summary>
    /// <typeparam name="T">Class object</typeparam>
    void Save<T>(string folderPath, string fileName, T content);
}