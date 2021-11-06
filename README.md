# dpostproc
 Decompilation post-processing

## VTBL Generator
This tool generates C++ source code from a supplied virtual table in the console.

### Example
Input:

```asm
__vt__Q24Game17EnemyAnimKeyEvent:
.4byte 0
.4byte 0
.4byte __dt__Q24Game17EnemyAnimKeyEventFv
.4byte getChildCount__5CNodeFv
```

Output:

```cpp
// NOTE: THE SCOPE AND FUNCTION OFFSETS OF ALL CLASSES BESIDES THE ONE YOU INPUTTED MAY BE WRONG!

namespace Game { // Unsure if namespace or structure!
struct CNode
{
        virtual void getChildCount(); // _00

        // _00, VTBL
};

struct EnemyAnimKeyEvent : public CNode
{
        ~EnemyAnimKeyEvent()          // _00, from Game::
        virtual void getChildCount(); // _04, from CNode::

        // _00, VTBL
};
} // namespace Game
```

The CNode class is not within the Game namespace, and as such this inconsistent scoping must be accounted for when implementing the tool's output.

## Shifter
This tool recognises and fixes hardcoded addresses in .s files, this "fix" is a simple annotation using a label and then referencing the label where the address is found.

You will have to do extra searches for false positives and .data to .data pointers, although this solution can decrease the amount of pointers from tens of thousands to hundreds, making it much quicker and easier for shiftability to occur (hence the name Shifter).

## Common Decomp Filler
This is a "multi-tool" of sorts, the tool is only made for <a href="https://github.com/intns/mapdas">mapdas</a> generated projects.
Features:
- Asks the user for known namespaces, for scope correction of functions
- Fixes constructor / destructor functions (would have "void" prefixed)
- Fixes certain improper const implementations from earlier versions of mapdas
- Recognises simple ASM patterns and fills the code in automatically, such as:
	- "blr" functions (an empty function)
	- "li" -> "blr" functions that reference r3, so as to simply return a numeric value (automatically sets return value to `u32` or `s32`)
	- "stw", "sth", "stb" functions where it can a) set a single member variable to an argument OR b) setting a single member variable to a constant
- Adds an #include "types.h" to the start of each .cpp file if needed, to make sure it is compatible with the automatically generated code