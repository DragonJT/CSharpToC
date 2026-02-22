#include <stdio.h>
#include "raylib.h"
#include "raymath.h"
#define RLIGHTS_IMPLEMENTATION
#include "rlights.h"
#include <stdlib.h>
typedef struct AlienGame {
Light light;
float radians;
Shader shader;
}AlienGame;

typedef struct Program {
}Program;

AlienGame* AlienGame_defaultConstructor_0()
{
    return (AlienGame*)malloc(sizeof(AlienGame));
}
                        
void AlienGame_Awake_1(AlienGame *this)
{
this->shader = LoadShader("c/lighting.vs", "c/lighting.fs");
this->shader.locs[SHADER_LOC_VECTOR_VIEW] = GetShaderLocation(this->shader, "viewPos");
int* ambientLoc = GetShaderLocation(this->shader, "ambient");
SetShaderValue(this->shader, ambientLoc, (float[4]){0.1, 0.1, 0.1, 1}, SHADER_UNIFORM_VEC4);
this->light = CreateLight(LIGHT_POINT, (Vector3){-5, 2, -5}, (Vector3){0, 0, 0}, YELLOW, this->shader);
}

void AlienGame_Update_2(AlienGame *this)
{
Camera3D camera = {0};
camera.position = (Vector3){(cosf(this->radians) * 10), 10, (sinf(this->radians) * 10)};
camera.target = (Vector3){0, 0, 0};
camera.up = (Vector3){0, 1, 0};
camera.fovy = 45;
camera.projection = CAMERA_PERSPECTIVE;
UpdateLightValues(this->shader, this->light);
float* cameraPos = (float[3]){camera.position.x, camera.position.y, camera.position.z};
SetShaderValue(this->shader, this->shader.locs[SHADER_LOC_VECTOR_VIEW], cameraPos, SHADER_UNIFORM_VEC3);
BeginMode3D(camera);
BeginShaderMode(this->shader);
DrawCube((Vector3){0, 0, 0}, 4, 1, 4, RED);
DrawCube((Vector3){0, 0, 0}, 2, 2, 2, BLACK);
EndShaderMode();
EndMode3D();
this->radians += (PI * GetFrameTime());
}

int main()
{
AlienGame* alienGame = AlienGame_defaultConstructor_0();
SetConfigFlags(FLAG_MSAA_4X_HINT);
InitWindow(800, 600, "Test");
AlienGame_Awake_1(alienGame);
while(!WindowShouldClose())
{
BeginDrawing();
ClearBackground(BLUE);
AlienGame_Update_2(alienGame);
DrawText("HELLO WORLD", 100, 100, 50, RAYWHITE);
EndDrawing();
}
}

