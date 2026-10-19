using Core.Domain;

namespace Core.Abstractions;

// Однакові правила для всіх сховищ: Id без урахування регістру;
// Add відхиляє дубль, Update — відсутній Id; читання повертає незалежні знімки.
public interface ICatalogStore
{
    IReadOnlyList<Goods> List();
    Goods? GetById(string id);
    void Add(Goods item);
    void Update(Goods item);
    bool Remove(string id);
}
