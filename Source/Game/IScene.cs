namespace Game;

public interface IScene
{
    void Update(float deltaTime);
    void Render();
    
    /// <summary>
    /// Called when this scene becomes the active scene.
    /// </summary>
    void OnEnter();
    
    /// <summary>
    /// Called when this scene is no longer the active scene.
    /// </summary>
    void OnExit();
}
