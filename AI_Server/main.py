import numpy as np
import matplotlib.pyplot as plt
from fastapi import FastAPI
import uvicorn
import random

app = FastAPI()

class IndustrialShapeGenerator:
    def __init__(self):
        self.VOID, self.FLOOR = 0, 1
        self.WALL_STRAIGHT = 2
        self.CORNER_EXT, self.CORNER_INT = 3, 4
        self.DOOR_IN, self.DOOR_OUT = 5, 6
        self.WALL_PLACEHOLDER = 7
        self.ROBOT_ARM = 8 # ID nou pentru brațul robotic
        
        self.cmap = {
            0: [1, 1, 1],       # Alb (Vid)
            1: [0.2, 0.4, 1],   # Albastru (Podea)
            2: [0, 0, 0],       # Negru (Perete)
            3: [1, 0.5, 0],     # Portocaliu (Colț Ext)
            4: [0.8, 0, 0.8],   # Roz (Colț Int/Pivot)
            5: [0, 1, 0],       # Verde (Intrare)
            6: [1, 0, 0],       # Roșu (Ieșire)
            7: [0.7, 0.7, 0.7], # Gri (Placeholder)
            8: [1, 1, 0]        # Galben (Braț Robotic)
        }

    def get_distance(self, p1, p2):
        return abs(p1[0] - p2[0]) + abs(p1[1] - p2[1])

    def apply_smart_walls(self, grid):
        rows, cols = grid.shape
        new_grid = grid.copy()
        for r in range(rows):
            for c in range(cols):
                if grid[r, c] == self.VOID:
                    f_card = sum(1 for dr, dc in [(-1,0),(1,0),(0,-1),(0,1)] 
                                 if 0<=r+dr<rows and 0<=c+dc<cols and grid[r+dr, c+dc] == self.FLOOR)
                    f_diag = sum(1 for dr, dc in [(-1,-1),(-1,1),(1,-1),(1,1)] 
                                 if 0<=r+dr<rows and 0<=c+dc<cols and grid[r+dr, c+dc] == self.FLOOR)

                    if f_card >= 2: new_grid[r, c] = self.CORNER_INT
                    elif f_card == 1: new_grid[r, c] = self.WALL_STRAIGHT
                    elif f_diag > 0: new_grid[r, c] = self.CORNER_EXT
        return new_grid

    def clean_corner_neighbors(self, grid):
        rows, cols = grid.shape
        final_grid = grid.copy()
        for r in range(rows):
            for c in range(cols):
                if grid[r, c] == self.CORNER_INT:
                    for dr, dc in [(-1,0),(1,0),(0,-1),(0,1)]:
                        nr, nc = r+dr, c+dc
                        if 0<=nr<rows and 0<=nc<cols and grid[nr, nc] == self.WALL_STRAIGHT:
                            final_grid[nr, nc] = self.WALL_PLACEHOLDER
        return final_grid

    def generate_legal_hall(self, min_corner_dist=3, min_door_dist=15):
        attempts = 0
        while attempts < 100:
            grid = np.zeros((25, 25), dtype=int)
            
            # 1. Nucleul halei
            w1, h1 = random.randint(12, 18), random.randint(12, 18)
            x1, y1 = random.randint(2, 25-w1-2), random.randint(2, 25-h1-2)
            grid[y1 : y1 + h1, x1 : x1 + w1] = self.FLOOR
            
            # 2. Extensii
            num_extensions = random.randint(1, 3)
            for _ in range(num_extensions):
                f_coords = np.argwhere(grid == self.FLOOR)
                anchor = random.choice(f_coords)
                w_ext, h_ext = random.randint(6, 12), random.randint(6, 12)
                x_ext = max(1, min(24-w_ext-1, anchor[1] - random.randint(0, w_ext-1)))
                y_ext = max(1, min(24-h_ext-1, anchor[0] - random.randint(0, h_ext-1)))
                grid[y_ext:y_ext+h_ext, x_ext:x_ext+w_ext] = self.FLOOR

            temp_grid = self.apply_smart_walls(grid)
            
            # 3. Verificare colțuri
            corners = np.argwhere((temp_grid == self.CORNER_EXT) | (temp_grid == self.CORNER_INT))
            corner_ok = True
            for i in range(len(corners)):
                for j in range(i + 1, len(corners)):
                    if self.get_distance(corners[i], corners[j]) < min_corner_dist:
                        corner_ok = False; break
            if not corner_ok:
                attempts += 1; continue

            # 4. Pivot și Uși
            temp_grid = self.clean_corner_neighbors(temp_grid)
            walls = np.argwhere(temp_grid == self.WALL_STRAIGHT)
            
            if len(walls) >= 2:
                door_placed = False
                for _ in range(100):
                    idx1, idx2 = random.sample(range(len(walls)), 2)
                    p1, p2 = walls[idx1], walls[idx2]
                    if self.get_distance(p1, p2) >= min_door_dist:
                        temp_grid[p1[0], p1[1]] = self.DOOR_IN
                        temp_grid[p2[0], p2[1]] = self.DOOR_OUT
                        door_placed = True
                        break
                
                if not door_placed:
                    attempts += 1; continue

                # 6. PLASARE BRAȚE ROBOTICE (Nou!)
                # Căutăm toate zonele de podea libere (ID 1)
                floor_tiles = np.argwhere(temp_grid == self.FLOOR)
                if len(floor_tiles) > 10: # Ne asigurăm că avem destul spațiu
                    num_robots = random.randint(1, 4)
                    # Alegem X indici aleatorii fără a repeta poziția
                    chosen_indices = random.sample(range(len(floor_tiles)), num_robots)
                    for idx in chosen_indices:
                        ry, rx = floor_tiles[idx]
                        temp_grid[ry, rx] = self.ROBOT_ARM
                
                return temp_grid
            attempts += 1
        return np.zeros((25,25))

gen = IndustrialShapeGenerator()

@app.get("/generate")
async def api_generate():
    matrix = gen.generate_legal_hall()
    return {"width": 25, "height": 25, "data": matrix.flatten().tolist()}

@app.get("/test_visual")
async def test_visual():
    matrix = gen.generate_legal_hall()
    vis = np.zeros((25, 25, 3))
    for r in range(25):
        for c in range(25): vis[r, c] = gen.cmap.get(matrix[r, c], [1, 0, 1])
    plt.figure(figsize=(8, 8))
    plt.imshow(vis, interpolation='nearest')
    plt.title("Hala XL cu Brațe Robotice (Galben)")
    plt.show()
    return {"status": "ok"}

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=8000)