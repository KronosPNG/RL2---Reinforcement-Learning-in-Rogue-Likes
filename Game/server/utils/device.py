import torch

DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")
print(f"[AI] Using device: {DEVICE}")
