using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [SerializeField] private Texture2D defaultCursor;
    [SerializeField] private Texture2D buildCursor;
    

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }
    
    void Start()
    {
        SetCursor(defaultCursor);
    }
    


    // ** Cursor
    public void SetCursor(Texture2D cursor, Vector2 hotspot = default(Vector2))
    {
        Cursor.SetCursor(cursor, hotspot, CursorMode.Auto);
    }
    
    public void SetDefaultCursor() => SetCursor(defaultCursor);
    public void SetBuildCursor() => SetCursor(buildCursor);
    
}
