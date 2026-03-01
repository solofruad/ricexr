# Diagrama de SceneInteractionManager en Mermaid

## 1. DIAGRAMA DE FLUJO COMPLETO

```mermaid
graph TD
    A["?? Usuario hace click<br/>en botón del plano"] -->|ButtonLogic.OnButtonClicked| B["SceneInteractionManager<br/>OnAnchorButtonClicked"]
  
    B -->|Valida hasBeenActivated| C{¿Ya fue<br/>activado?}
    C -->|Sí| D["? Return<br/>Evita clicks múltiples"]
    C -->|No| E["? Guarda parámetros<br/>targetPosition<br/>targetRotation<br/>targetScale"]
    
    E -->|hasBeenActivated = true| F["StartCoroutine<br/>DisableAllPlanePrefabsWithAnimation"]
    
    F -->|Obtiene| G["MRUK.Instance<br/>GetCurrentRoom"]
    G -->|Si null| H["?? Debug.LogWarning<br/>yield break"]
    G -->|Si valid| I["Itera room.Anchors"]
 
    I -->|Por cada anchor| J["FindPlanePrefab<br/>Busca en jerarquía"]
    J -->|Encuentra| K["Añade a lista<br/>planePrefabs"]
    J -->|No encuentra| L["Continúa siguiente"]
    
    K -->|Para cada plano| M["StartCoroutine<br/>AnimatePlanePrefabScaleDown"]
    M -->|Anima escala| N["while elapsed < scaleDuration"]
    N -->|Cada frame| O["t = elapsed / scaleDuration<br/>easedT = EaseOutCubic"]
    O -->|Lerp| P["localScale = Lerp<br/>original ? Vector3.zero"]
    P -->|Si elapsed >= duration| Q["localScale = (0,0,0)"]
    
    Q -->|Espera| R["WaitForSeconds<br/>scaleDuration"]
    R -->|SetActive false| S["Desactiva todos<br/>los planos"]
    
    S -->|Llama| T["SpawnNewObject"]
  
    T -->|Valida| U{¿prefabsToSpawn<br/>está vacío?}
    U -->|Sí| V["?? Debug.LogWarning<br/>return"]
    U -->|No| W{¿currentLevelIndex >=<br/>prefabsToSpawn.Count?}
    
    W -->|Sí| X["? Debug.Log<br/>Todos niveles completados"]
    W -->|No| Y["Obtiene prefab<br/>prefabsToSpawn[currentLevelIndex]"]
  
    Y -->|Si null| Z["?? Debug.LogWarning<br/>return"]
    Y -->|Si valid| AA["ConfigureLeafSpawn"]
    
    AA -->|Configura posición| AB["parent.rotation = targetRotation"]
 AA -->|Instancia| AC["Instantiate prefab"]
  AA -->|Posiciona| AD["position = targetPosition<br/>- Vector3.up * 0.03f"]
    AA -->|Rota| AE["rotation = targetRotation"]
    
    AA -->|Obtiene componente| AF["LeavesSpawner leafSpawner"]
    AF -->|Si null| AG["?? Debug.LogWarning"]
    AF -->|Si valid| AH["Configura SpawnAreaSize<br/>= targetScale"]
    
    AH -->|Configura parent| AI["leafSpawner.grassParent = parent"]
    AI -->|Si grassCount == -1| AJ["Calcula automáticamente<br/>Max de 20 hojas"]
    AJ -->|Llama| AK["leafSpawner.Activate"]
    
    AK -->|StartCoroutine| AL["InitializeLeaves<br/>Comienza spawn de hojas"]
  
style A fill:#ff6b6b
    style B fill:#4ecdc4
    style E fill:#95e1d3
    style F fill:#f38181
    style T fill:#ffe66d
    style AK fill:#a8d8ea
```

---

## 2. DIAGRAMA DE ESTRUCTURA DE CLASES Y PROPIEDADES

```mermaid
classDiagram
    class SceneInteractionManager {
        -Instance: static SceneInteractionManager
        -prefabsToSpawn: List~GameObject~
  -spawnParent: Transform
        -currentLevelIndex: int
     -scaleDuration: float
        -targetPosition: Vector3
    -targetRotation: Quaternion
    -targetScale: Vector3
        -hasBeenActivated: bool
        -currentLeavesContainer: GameObject
        
        +Awake() void
        +OnAnchorButtonClicked(Vector3, Quaternion, Vector3) void
        -DisableAllPlanePrefabsWithAnimation() IEnumerator
        -AnimatePlanePrefabScaleDown(GameObject) IEnumerator
        -FindPlanePrefab(Transform) Transform
        -SpawnNewObject() void
        -ConfigureLeafSpawn(GameObject) void
        +AdvanceToNextLevel() void
        -DestroyCurrentLeaves() void
        +ResetToFirstLevel() void
    -OnAllLevelsCompleted() void
        -EaseOutCubic(float) float
        +GetCurrentLevelIndex() int
        +GetTotalLevels() int
        +GetLevelProgress() string
    }
    
    class LeavesSpawner {
        +SpawnAreaSize: Vector2
        +grassCount: int
        +grassParent: Transform
        +Activate() void
        +InitializeLeaves() IEnumerator
        +AnimateAllLeaves() IEnumerator
    }
    
    class MRUK {
+Instance: static MRUK
  +GetCurrentRoom() MRUKRoom
    }
    
    class MRUKRoom {
        +Anchors: List~MRUKAnchor~
    }

    class MRUKAnchor {
        +gameObject: GameObject
        +transform: Transform
    }
    
    SceneInteractionManager "1" -- "1" LeavesSpawner: configura
    SceneInteractionManager "1" -- "1" MRUK: obtiene
    MRUK "1" -- "1" MRUKRoom: proporciona
    MRUKRoom "1" -- "*" MRUKAnchor: contiene
```

---

## 3. DIAGRAMA DE PROGRESIÓN DE NIVELES

```mermaid
stateDiagram-v2
    [*] --> Esperando_Click
  
    Esperando_Click --> Animando_Planos: Usuario clickea plano
  Esperando_Click --> [*]: Reset
    
    Animando_Planos --> Buscando_PlanePrefabs: Obtiene anchors
    Buscando_PlanePrefabs --> Animando_Desaparición: Encuentra planos
    Animando_Desaparición --> Desactivando_Planos: Espera duración
    
    Desactivando_Planos --> Spawning_Hojas: SetActive false
    Spawning_Hojas --> Configurando_Hojas: ConfigureLeafSpawn
    Configurando_Hojas --> Crecimiento_Hojas: Activate LeavesSpawner
    
    Crecimiento_Hojas --> Esperando_Diagnosis: Hojas crecen
    Esperando_Diagnosis --> Nivel_Completado: Usuario diagnostica correcto
    Esperando_Diagnosis --> Esperando_Diagnosis: Usuario diagnostica incorrecto
    
    Nivel_Completado --> Destruyendo_Hojas: AdvanceToNextLevel
    Destruyendo_Hojas --> currentLevelIndex++
    currentLevelIndex++ --> ¿Más_Niveles?
    
    ¿Más_Niveles? --> Esperando_Click: Sí ? SpawnNewObject
    ¿Más_Niveles? --> Fin_Experiencia: No ? OnAllLevelsCompleted
    Fin_Experiencia --> [*]
```

---

## 4. DIAGRAMA SECUENCIAL: DESDE CLICK HASTA SPAWN

```mermaid
sequenceDiagram
    participant User as Usuario
    participant Button as ButtonLogic
    participant Manager as SceneInteractionManager
    participant MRUK as MRUK/MRUKRoom
    participant Plane as PlanePrefab
    participant Spawner as LeavesSpawner
    
    User->>Button: Click en botón del plano
    activate Button
    Button->>Manager: OnAnchorButtonClicked(pos, rot, scale)
    deactivate Button
    
    activate Manager
  Manager->>Manager: Valida hasBeenActivated
    Manager->>Manager: Guarda targetPosition, targetRotation, targetScale
    Manager->>Manager: hasBeenActivated = true
    Manager->>Manager: StartCoroutine DisableAllPlanePrefabsWithAnimation()
    
    activate Manager
    Manager->>MRUK: GetCurrentRoom()
    activate MRUK
    MRUK-->>Manager: MRUKRoom
    deactivate MRUK
    
    loop Para cada anchor en room.Anchors
Manager->>Manager: FindPlanePrefab(anchor.transform)
        Manager->>Manager: Añade a lista planePrefabs
    end
    
    loop Para cada planePrefab encontrado
   Manager->>Manager: StartCoroutine AnimatePlanePrefabScaleDown()
        activate Manager
        loop Mientras elapsed < scaleDuration
       Manager->>Plane: localScale = Lerp(original, zero, easedT)
       Plane-->>Manager: animando...
        end
        Manager->>Plane: localScale = Vector3.zero
    deactivate Manager
    end
    
    Manager->>Manager: WaitForSeconds(scaleDuration)
    Manager->>Plane: SetActive(false)
    Manager->>Manager: SpawnNewObject()

    activate Manager
    Manager->>Manager: Obtiene prefab del nivel
    Manager->>Manager: ConfigureLeafSpawn(prefab)
  
    activate Manager
    Manager->>Manager: parent.rotation = targetRotation
    Manager->>Spawner: Instantiate(prefab)
    Spawner-->>Manager: nueva instancia
    Manager->>Spawner: position = targetPosition - Vector3.up * 0.03f
    Manager->>Spawner: rotation = targetRotation
    Manager->>Spawner: SpawnAreaSize = targetScale
    Manager->>Spawner: grassParent = parent
    Manager->>Spawner: Calcula grassCount si es -1
    Manager->>Spawner: Activate()
    activate Spawner
    Spawner->>Spawner: StartCoroutine InitializeLeaves()
    Spawner-->>Manager: ? Proceso iniciado
    deactivate Spawner
    deactivate Manager
    deactivate Manager
    deactivate Manager
```

---

## 5. DIAGRAMA DE MÉTODOS Y RESPONSABILIDADES

```mermaid
graph LR
  A["OnAnchorButtonClicked"] -->|punto de entrada| B["DisableAllPlanePrefabsWithAnimation"]
    
    B -->|busca| C["FindPlanePrefab"]
    B -->|anima| D["AnimatePlanePrefabScaleDown"]
    B -->|desactiva| E["SetActive false"]
    B -->|llama| F["SpawnNewObject"]
    
    F -->|valida| G["Comprueba lista prefabs"]
    F -->|valida| H["Comprueba índice nivel"]
  F -->|configura| I["ConfigureLeafSpawn"]
    
    I -->|posiciona| J["Calcula Transform"]
    I -->|obtiene componente| K["GetComponent LeavesSpawner"]
    I -->|configura| L["SpawnAreaSize<br/>grassCount<br/>grassParent"]
    I -->|inicia| M["Activate LeavesSpawner"]
    
    N["AdvanceToNextLevel"] -->|destruye| O["DestroyCurrentLeaves"]
    N -->|incrementa| P["currentLevelIndex++"]
    N -->|verifica| Q["¿Más niveles?"]
  Q -->|Sí| F
    Q -->|No| R["OnAllLevelsCompleted"]
    
    S["ResetToFirstLevel"] -->|destruye| O
    S -->|resetea| T["currentLevelIndex = 0"]
    S -->|spawnea| F
    
    U["EaseOutCubic"] -->|función helper| D
    V["GetCurrentLevelIndex"] -->|getter| W["Información del nivel"]
    X["GetTotalLevels"] -->|getter| W
    Y["GetLevelProgress"] -->|getter| W
```

---

## 6. DIAGRAMA DE FLUJO DE DATOS

```mermaid
graph TD
    A["ButtonLogic envía:<br/>position, rotation, scale"] -->|OnAnchorButtonClicked| B["SceneInteractionManager<br/>guarda temporalmente"]
    
B -->|targetPosition| C["Se usa en<br/>ConfigureLeafSpawn"]
    B -->|targetRotation| C
    B -->|targetScale| C
    
    C -->|configura| D["LeavesSpawner instance"]
    D -->|SpawnAreaSize| E["LeavesSpawner.InitializeLeaves"]
    D -->|grassParent| E
    D -->|grassCount| E
    
    E -->|genera posiciones| F["GenerateSpawnPositions"]
    E -->|instancia| G["Leaf prefabs"]
    
    G -->|crecen en| H["AnimateAllLeaves<br/>corrutina"]
    
    I["currentLevelIndex"] -->|determina| J["prefabsToSpawn[index]"]
    J -->|qué prefab| C
    
    K["AdvanceToNextLevel"] -->|incrementa| I
 L["ResetToFirstLevel"] -->|resetea| I
```

---

## 7. TABLA DE TRANSICIONES DE ESTADO

| Estado | Evento | Acción | Nuevo Estado |
|--------|--------|--------|--------------|
| Esperando | Click | OnAnchorButtonClicked | Animando Planos |
| Animando Planos | Tiempo transcurrido | DisableAllPlanePrefabsWithAnimation | Spawning Hojas |
| Spawning Hojas | Prefab instanciado | ConfigureLeafSpawn | Crecimiento Hojas |
| Crecimiento Hojas | LeavesSpawner.Activate | AnimateAllLeaves | Diagnosis |
| Diagnosis | Usuario diagnostica correcto | AdvanceToNextLevel | Destruyendo/Esperando |
| Diagnosis | Usuario diagnostica incorrecto | ShowFeedback | Diagnosis |
| Destruyendo | Hojas destruidas | currentLevelIndex++ | ¿Más Niveles? |
| ¿Más Niveles? | Sí | SpawnNewObject | Esperando |
| ¿Más Niveles? | No | OnAllLevelsCompleted | Fin |

---

## 8. DIAGRAMA DE DEPENDENCIAS EXTERNAS

```mermaid
graph TB
    A["SceneInteractionManager<br/>Singleton"] -->|depende de| B["MRUK<br/>Meta.XR.MRUtilityKit"]
    A -->|depende de| C["LeavesSpawner"]
    A -->|depende de| D["ButtonLogic"]
    
    B -->|proporciona| E["MRUKRoom"]
    E -->|contiene| F["MRUKAnchor"]
 F -->|tiene| G["PlanePrefab<br/>Visual"]
    
    C -->|hereda| H["MonoBehaviour"]
  D -->|hereda| H
    A -->|hereda| H
    
    A -->|usa| I["Corrutinas<br/>Time.deltaTime<br/>Vector3.Lerp"]
    A -->|usa| J["Easing<br/>EaseOutCubic"]
    
  style A fill:#4ecdc4
    style C fill:#95e1d3
    style B fill:#ff6b6b
```

---

## 9. PSEUDO-CÓDIGO DEL FLUJO PRINCIPAL

```
CLASE SceneInteractionManager (Singleton):

 CUANDO Usuario Clickea Plano:
     1. OnAnchorButtonClicked(pos, rot, scale)
  2. SI hasBeenActivated == false:
  a. Guardar parámetros (pos, rot, scale)
       b. hasBeenActivated = true
       c. Iniciar DisableAllPlanePrefabsWithAnimation()
   3. SINO: Ignorar
    
    EN DisableAllPlanePrefabsWithAnimation:
        1. Obtener MRUKRoom desde MRUK.Instance
        2. PARA cada anchor en room.Anchors:
   a. Buscar PlanePrefab en jerarquía
        b. INICIAR AnimatePlanePrefabScaleDown(plano)
        3. ESPERAR scaleDuration segundos
      4. Desactivar todos los planos encontrados
        5. Llamar SpawnNewObject()
    
    EN AnimatePlanePrefabScaleDown:
        1. Guardar escala original
        2. MIENTRAS elapsed < scaleDuration:
            a. Calcular t normalizado (0-1)
    b. Aplicar easing: easedT = EaseOutCubic(t)
   c. Lerp escala: de original a Vector3.zero
         d. Esperar próximo frame
     3. Asegurar localScale = Vector3.zero
  
    EN SpawnNewObject:
      1. VALIDAR prefabsToSpawn no vacío
        2. VALIDAR currentLevelIndex < prefabsToSpawn.Count
        3. Obtener prefab del nivel actual
4. Llamar ConfigureLeafSpawn(prefab)
    
    EN ConfigureLeafSpawn:
        1. Configurar parent transform
        2. Instantiate prefab en la ubicación
        3. Obtener componente LeavesSpawner
        4. CONFIGURAR:
            - SpawnAreaSize = targetScale
      - grassParent = parent
            - grassCount = cálculo automático SI -1
        5. Llamar leafSpawner.Activate()
    
    CUANDO AdvanceToNextLevel:
        1. Destruir todas las hojas actuales
        2. Incrementar currentLevelIndex
  3. SI currentLevelIndex >= prefabsToSpawn.Count:
            - Llamar OnAllLevelsCompleted()
 4. SINO:
      - Llamar SpawnNewObject()
    
    CUANDO ResetToFirstLevel:
        1. Destruir todas las hojas actuales
        2. currentLevelIndex = 0
        3. Llamar SpawnNewObject()
```

