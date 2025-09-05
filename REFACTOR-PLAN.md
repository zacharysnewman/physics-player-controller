# PlayerController Refactor Plan

## Current Issues

The current `PlayerController` implementation uses a Unity antipattern where logic is separated into utility classes but they're only used internally within the main controller. This violates the Single Responsibility Principle and creates tight coupling.

### Problems Identified:
1. **God Object Anti-pattern**: `PlayerController` manages input, movement, ground detection, camera control, and debugging
2. **Tight Coupling**: All utility classes are instantiated internally and have no independent existence
3. **Poor Reusability**: Components cannot be used independently or mixed/matched
4. **Difficult Testing**: Logic is buried within monolithic controller
5. **No Component Communication**: Everything is handled through direct method calls within one class

## Refactor Goals

1. **Follow Single Responsibility Principle**: Each component should have one clear responsibility
2. **Create Independent Components**: Convert utility classes to `MonoBehaviour` components that can be attached to GameObjects
3. **Establish Component Relationships**: Use proper Unity component communication patterns
4. **Improve Reusability**: Components should be usable independently or in different combinations
5. **Better Testability**: Components should be easily testable in isolation

## Proposed Component Architecture

### 1. PlayerInput Component
**File**: `PlayerInput.cs` (MonoBehaviour)
**Responsibility**: Handle all input system setup and input state management
**Configuration**:
- InputActions asset reference
- Input sensitivity settings
- Input response curves

**Dependencies**: None
**References**: Other components that need input data

### 2. PlayerMovement Component
**File**: `PlayerMovement.cs` (MonoBehaviour)
**Responsibility**: Handle character movement physics and locomotion
**Configuration**:
- Movement speeds (walk, run)
- Acceleration/deceleration rates
- Jump parameters
- Ground movement settings

**Dependencies**: Requires Rigidbody
**References**: GroundChecker (for ground state), PlayerInput (for input)

### 3. GroundChecker Component
**File**: `GroundChecker.cs` (MonoBehaviour)
**Responsibility**: Detect ground, ceiling, and wall collisions
**Configuration**:
- Check distances and radii
- Layer masks for different surfaces
- Slope detection parameters

**Dependencies**: Requires CapsuleCollider
**References**: None (provides data to other components)

### 4. CameraController Component
**File**: `CameraController.cs` (MonoBehaviour)
**Responsibility**: Handle camera movement and rotation
**Configuration**:
- Mouse sensitivity
- Vertical angle limits
- Camera follow settings

**Dependencies**: Requires Camera reference
**References**: PlayerInput (for look input)

### 5. PlayerJump Component
**File**: `PlayerJump.cs` (MonoBehaviour)
**Responsibility**: Handle jump mechanics (buffer, coyote time, apex calculation)
**Configuration**:
- Jump force
- Buffer and coyote times
- Jump height calculations

**Dependencies**: Requires Rigidbody
**References**: GroundChecker (for ground state), PlayerInput (for jump input)

### 6. DebugVisualizer Component
**File**: `DebugVisualizer.cs` (MonoBehaviour)
**Responsibility**: Handle debug visualization and logging
**Configuration**:
- Visualization toggles
- Debug logging levels
- Gizmo display options

**Dependencies**: None
**References**: All other components (for debug data)

### 7. PlayerController (Coordinator)
**File**: `PlayerController.cs` (MonoBehaviour)
**Responsibility**: Coordinate between components and manage high-level player state
**Configuration**:
- Component references
- Overall player settings

**Dependencies**: Requires all player components
**References**: All components (for coordination)

## Component Communication Strategy

### Method 1: Direct Component References
- Components hold references to other components they need
- Direct method calls for immediate communication
- Best for: Performance-critical, tightly-coupled interactions

### Method 2: Unity Events
- Components emit events for state changes
- Other components subscribe to relevant events
- Best for: Loose coupling, multiple listeners

### Method 3: ScriptableObject Events
- Global event system using ScriptableObjects
- Components can emit/receive events without direct references
- Best for: Cross-scene communication, complex interactions

## Implementation Phases

### Phase 1: Core Component Conversion
1. Convert `PlayerInputHandler` → `PlayerInput` (MonoBehaviour)
2. Convert `PlayerMovement` → `PlayerMovement` (MonoBehaviour)
3. Convert `GroundChecker` → `GroundChecker` (MonoBehaviour)
4. Convert `CameraController` → `CameraController` (MonoBehaviour)
5. Create new `PlayerJump` component (extracted from PlayerMovement)
6. Convert `DebugVisualizer` → `DebugVisualizer` (MonoBehaviour)

### Phase 2: Component Integration
1. Establish component reference patterns
2. Implement component communication interfaces
3. Update component initialization and lifecycle

### Phase 3: Coordinator Refactor
1. Simplify `PlayerController` to coordinate role
2. Remove internal utility class instantiations
3. Add component dependency management

### Phase 4: Configuration and Prefabs
1. Create component configuration ScriptableObjects
2. Update prefabs to use component-based structure
3. Add component validation and error handling

## Benefits of Refactor

1. **Modularity**: Each component can be developed, tested, and maintained independently
2. **Reusability**: Components can be mixed and matched for different player types
3. **Testability**: Individual components can be unit tested in isolation
4. **Performance**: Components can be enabled/disabled independently
5. **Unity Best Practices**: Proper use of MonoBehaviour lifecycle and component system

## Migration Strategy

1. **Parallel Implementation**: Create new component-based system alongside existing system
2. **Gradual Migration**: Migrate functionality piece by piece
3. **Backward Compatibility**: Keep old system working during transition
4. **Testing**: Thoroughly test each component and their interactions
5. **Documentation**: Update all documentation and examples

## Potential Challenges

1. **Component Dependencies**: Managing complex dependency chains
2. **Performance Overhead**: Component communication vs direct calls
3. **Configuration Management**: Handling component settings across different use cases
4. **Prefab Updates**: Updating existing prefabs to use new component structure
5. **Testing Complexity**: Testing component interactions vs monolithic testing

## Success Criteria

1. All existing functionality preserved
2. Components are independently testable
3. Components can be reused in different combinations
4. Performance is maintained or improved
5. Code is more maintainable and readable
6. Unity best practices are followed